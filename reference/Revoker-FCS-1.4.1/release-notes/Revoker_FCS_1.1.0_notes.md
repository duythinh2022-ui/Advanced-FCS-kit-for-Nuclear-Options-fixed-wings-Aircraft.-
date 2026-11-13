# FS-12 Revoker FCS 1.1.0: the F-22E's performance work (Rust allocator, sweep cache, Rust grid kernels, tick segments)

Base: 1.0.1. **No control-law change.** Port of F-22E 4.0.0 – 4.1.3 (`claude/FCS_perf_port_handoff_2026-11-11.md`).

**Changelog:** The control allocator's solve, the surface tables' maths and the asymmetric-load sweep's inner loop now run in a native library (Revoker_FcsCore.dll, Rust, ported from the F-22E FCS), and the sweep works out each deflection's geometry once per tick instead of once per AoA point; same flying, less CPU time per tick. The tick timer now splits the whole tick into 13 segments, reports garbage collections, logs slow ticks and names the performance switches in force.

**Status:**
- Compiled against the game's assemblies (the plugin and `Revoker_FcsCore.dll`); plugin built twice, same bytes.
- Bench (.NET 8): every per-case file and suite table identical to 1.0.1 in all five configurations below.
- Game's Mono runtime under Wine, Unity's float mode: the plugin loads `Revoker_FcsCore.dll` by its full path, the ABI number and structure sizes match, the kernels' self-test passes (200 / 200 runs). Also checked with the F-22E 4.1.3, Revoker, Compass, Su-30 and Ifrit plugins all loaded in one process: each bound its own library.
- **Not flown.** In-game timing not measured yet (needs your log; see below).

## Tom
- 11 Nov 10:40: "do this for every fcs mods, except ternion" (the handoff: "gimme handoff so the optimization can be ported to other fcs jets")

## What is ported (all from the F-22E, as the handoff lists it)
| piece | F-22E | here |
|---|---|---|
| Rust allocator: `Allocator.Solve` line for line (reach pass, two-horizon pick, `SolveCore`, paired search, linear solve, `Effector.MAt`) | 4.0.0 | this jet's own `Allocator.cs`, see below |
| Sweep geometry cache (`SweepGridCache`): each deflection's rotations once per sweep tick | 4.0.1 | yes |
| Grid kernels (`rev_fcs_grid`): mode 0 table rebuild, mode 1 sweep min / max, with the load-time self-test against a C# twin | 4.1.0 | yes |
| Defaults `NativeAllocator 2`, `NativeAllocatorCheck 0` | 4.1.0 | new keys, so their defaults apply (no migration) |
| Tick segments, heap / GC, slow-tick log, switch mask, Telemetry on / off in flight | 4.1.3 | yes (log lines say "Revoker FCS") |

**This jet's differences from the F-22E's core** (in both `native/fcs_core/src/lib.rs` and `FcsNative.cs`):
- **Allocator:** the velocity-frame lateral residual (0.4.0: `slipFrame`, `slipSin`, `slipCos`) and the coordinate pass's Newton candidate for the yaw nozzles too (`TvcYawEffector`, `EffN.kind` 3).
- **Tables:** each grid point's normal force (`FN`, used by the trailing-edge brake config's lift balance) comes back from the kernel with the moment.
- **Sweep:** the canards are swept at their stock ±`AsymStabTravel` (0.2.7), not at their table grid, so the geometry cache is built for exactly the deflections the loop evaluates, and reuses the table's geometry only where the two lists match (the elevons, the rudder).

**Port fix, not in the F-22E 4.1.x:** `FcsNative.Load` published the library as ready before the kernels' self-test had run, so a table thread arriving during the self-test (first tick only) saw the solve ready and the kernels "not tested yet", and ran that surface's table in C# for that tick. Found because the kernel-call totals differed by a few between otherwise identical bench runs (outputs identical: same bits on the bench; in the game it would be one tick of Mono arithmetic). Now "ready" is published after the kernel test; other threads wait on the load's lock. The F-22E 4.1.x has the same first-tick race; harmless, but worth the same one-line move next time it is built. Regression block run twice on the release build: 132,780 and 132,780 kernel calls, 98,485 and 98,485 native solves (identical).

**Own names:** `Revoker_FcsCore.dll`, `DllImport("Revoker_FcsCore")`, exports `rev_fcs_solve / _grid / _abi / _sizes / _grid_size`. Loaded by full path before the first call (as the F-22E); any failure (missing DLL, ABI, sizes, an error return, a failed self-test) falls back to the C# code with one warning in the log.

## Verification
**Bench (.NET 8, strict float), the full suite (127 per-case files: regression set, stop / reversal sweep, corkscrews, damage set), against 1.0.1:**

| run | per-case files identical (timing columns aside) | suite tables | native |
|---|---|---|---|
| default (everything native) | 127 / 127 | 4 / 4 | 514,422 native solves; 675,202 kernel calls; 0 left to C# |
| `NativeAllocatorCheck=2` (every solve both ways) | 127 / 127 | 4 / 4 | 514,422 solved both ways, 514,422 identical, 0 different; 675,217 kernel calls; 0 left to C# |
| `NativeAllocator=0` (all C#) | 127 / 127 | 4 / 4 | C# only, library not loaded |
| `NativeSweep=false NativeTables=false` | 127 / 127 | 4 / 4 | 514,422 native solves; 0 left to C# |
| `SweepGridCache=false` | 127 / 127 | 4 / 4 | 514,422 native solves; 250,092 kernel calls; 0 left to C# |
| **release build**, defaults (with the load-order fix above) | 127 / 127 | 4 / 4 | 514,422 native solves; 675,465 kernel calls; 0 left to C# |

- "Timing columns aside": the CSV's wall-clock columns (`fcsUs` … `tVmcUs`) and the 16 new ones (`sgJoin` … `swMask`) are not compared; every other column is compared as text.

**Game's Mono (Unity's `-O=-float32`), the release DLLs:** loads, `Revoker_FcsCore.dll` found beside the plugin, ABI and sizes OK, kernel self-test passed 200 / 200. As on the F-22E, in the game the native code is the bench's arithmetic, not Mono's: the C# solve on Mono differs from it in the last digits and some equal-cost picks. Not measured on this jet's recorded solves (the F-22E's recording harness is F-22E-specific); on the F-22E that was 7.1 % identical, 68.4 % within 0.001°, 24.2 % equal-cost alternatives, 0.3 % different, and your F-22E flights showed fewer surface flip-backs with native (0 against 30 – 200 per minute), not more (not proven as the cause).

## Settings (Dev, "0. Debug"; all take effect at once)
| key | default | |
|---|---|---|
| `NativeAllocator` | 2 | 2 = native in the game; 1 = only where bit-identical (the bench; in the game = C#); 0 = C# as 1.0.1 |
| `NativeAllocatorCheck` | 0 | 1 = sampled solves both ways, 2 = every solve both ways (C# result used; counts in the log) |
| `NativeTables` / `NativeSweep` | on | the table rebuild / the sweep's capacity loop in the library |
| `SweepGridCache` | on | the sweep's geometry once per tick (C#, any runtime) |
| `SlowTickLogUs` | 1500 | log any player tick slower than this with its 13 segments (0 = off) |

## The flight test (what gives the real numbers)
The gains are measured from the timing log, as on the F-22E. Same routine as F-22E 4.1.3: `TickTimerLog` on (default), `Telemetry` on; repeat a full pull, a few full-stick rolls and reversals, a corkscrew, a push-over, ~30 s level. Hold each step ≥ 30 s (one clean 10 s window), then set it back:

| step | change | measures |
|---|---|---|
| 1 | all defaults | baseline |
| 2 | `NativeTables` off | Rust tables vs C# |
| 3 | `NativeSweep` off | Rust sweep vs C# |
| 4 | `SweepGridCache` off | the geometry cache |
| 5 | `NativeAllocator` 0 | Rust allocator vs C# |
| 6 | back to defaults | drift check |

The 10 s line `Revoker FCS timing, whole tick in segments …` names the switches and flags a window where one changed. One mission with AI Revokers as well: the native path runs per controller, so AI jets use it too; look for warnings in `LogOutput.log`.

## Build notes
- Rust 1.97.1 (Ubuntu's package) with the Windows standard library compiled from source (`-Zbuild-std`): the prebuilt one could not be downloaded here. `native/build.sh` says how. The F-22E's DLL was built with 1.99.1; the code uses nothing version-specific.
- The plugin was compiled with this environment's .NET 8 SDK compiler; rebuilding 1.0.1 from its source here gives different bytes from the shipped 1.0.1 DLL (compiler build), so the bench output, not the DLL bytes, is the identity check.
- Source additions: `native/` (the crate, `build.sh`, `README.md`, `verify/`: `host.c`, `jetcheck.cs`, `multicheck.cs`, `csvcmp.py`, `nsum.py`), `FcsNative.cs`, the bench's `FCS_NATIVE_STATS` and its Linux library build in `bench.sh build`.

## Install
- **Main:** `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` 1.1.0 + **`Revoker_FcsCore.dll` beside it** + `meta.json`. 1.0.1 kept as `Revoker_FS12_FCS.dll.1.0.1.bak`.
- **Dev:** unchanged (no code). The new keys appear by themselves.
- **Release:** `C:\Games\NO mods\Revoker-FCS-1.1.0-release`. The mod zip now holds the plugin DLL, `Revoker_FcsCore.dll` and `meta.json`.
- **Mod manager:** update the version there, or its modpack may put the old DLL back (and it must carry `Revoker_FcsCore.dll` too).
