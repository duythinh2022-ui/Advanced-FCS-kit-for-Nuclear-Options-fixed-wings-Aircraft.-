# FS-3 Ternion FCS 0.3.0: the F-22E's performance work, the coupled-cap solver in Rust, one DLL

Base: 0.2.3. **No law change:** on the bench every output is bit-identical to 0.2.3. This is the port the other jets got on 11 Nov (`claude/FCS_perf_port_results_2026-11-11.md`), plus the Ternion's own coupled asymmetric-load check moved into the native core, shipped as a single DLL.

**Changelog:** The FCS's heavy maths now runs in a native (Rust) core carried inside the plugin's DLL: the control allocator, the surface tables, the asymmetric-load sweep, and the coupled check's solver that 0.2.2 added. Same flying, less CPU time per tick.

**Status:** compiled, benched (bit-identical to 0.2.3), run on the game's own Mono runtime under Wine. **Not flown; in-game timing not measured.**

## What is in it
| piece | from | what it does |
|---|---|---|
| Rust allocator (`tern_fcs_solve`) | F-22E 4.0.0, via the Compass 0.3.0 port | the allocator's solve, line for line. The Ternion's only difference from the Compass (`fightHintOff`, 0.1.9) reaches the core as a zero hint threshold. |
| Sweep geometry cache (`SweepGridCache`) | F-22E 4.0.1 | each deflection's rotation once per sweep tick, not once per AoA point (C#) |
| Grid kernels (`tern_fcs_grid`: `NativeTables`, `NativeSweep`) | F-22E 4.1.0 | one surface's moment at every grid deflection in one call |
| Tick segments, heap / GC, slow-tick log, live `Telemetry` | F-22E 4.1.3 | the measuring tool: the 10 s timer line names 13 segments and the switches |
| **Coupled-cap solver in Rust** (`tern_fcs_cpl`, `NativeCoupledCap`) | new, Ternion | `CoupledCap.cs`'s LP (`Reserve`, `LateralFeasible`, `PitchHeld` and their simplex), expression for expression, on the C# side's own arrays |
| **Coupled-check points cached** (`CplGridCache`) | new, Ternion | the check's 9 points per surface: rotations once per sweep tick; the attached-side test reads the same rotation; with the kernels, all 9 moments in one native call |
| **One DLL** | as the other jets' one-DLL builds | `Ternion_FcsCore.dll` travels inside `Ternion_FS3_FCS.dll` and is written once to `BepInEx\cache\Ternion_FcsCore\<hash>\` |

How the Compass's port was moved over: a three-way merge of Compass 0.2.1 → 0.3.0 onto the Ternion's source (7 small conflicts, all hand-resolved: version, log header, two Ternion-only lines). The core has its own names (`Ternion_FcsCore`, `tern_fcs_`), so it cannot collide with the other jets' cores in the same game.

## Verification
**Bench (.NET 8, strict float arithmetic), against 0.2.3. "Identical" = every column of every per-case log as text, wall-clock timing columns aside.**

| run | suite (125 per-case logs, 4 tables) | loaded reversals (88 logs) | negative-AoA loaded set (50 logs) |
|---|---|---|---|
| default (everything native) | 125 / 125, 4 / 4 | 88 / 88 | 50 / 50 |
| `NativeAllocatorCheck=2` (every solve and every LP call both ways) | 125 / 125, 4 / 4 | 88 / 88 | 50 / 50 |
| `NativeAllocator=0` (all C#) | 125 / 125, 4 / 4 | — | 50 / 50 |
| kernels and LP in C# (`NativeSweep`, `NativeTables`, `NativeCoupledCap` off) | 125 / 125, 4 / 4 | — | — |
| `SweepGridCache=false` | 125 / 125, 4 / 4 | — | — |
| `CplGridCache=false` | — | — | 50 / 50 |
| final build, defaults (after the loader change) | 125 / 125, 4 / 4 | — | 50 / 50 |

Both-ways counts (`NativeAllocatorCheck=2`), all three sets together: **1,486,910 allocator solves, 1,486,910 identical; 640,625 coupled-cap LP calls, 640,625 identical; 0 different.** Default runs: 0 calls left to C#, 0 fall-backs (suite 503,751 native solves and 1,379,040 kernel calls; loaded reversals 645,353 / 2,812,240 / 424,972 LP calls).

- The loaded sets matter here: the clean suite barely runs the coupled check (1,005 LP calls, in the damage cases); the loaded sets run it 220,000 – 425,000 times each.
- Some damage cases fail before and after (the damage map names parts this jet does not have); they fail identically.

**Game's own Mono (`mono-2.0-bdwgc.dll` with Unity's `-O=-float32`, under Wine), the release DLL:**
| check | result |
|---|---|
| one-file install under a `BepInEx` folder | core written to `BepInEx\cache\Ternion_FcsCore\6caf95f383acdd4d\Ternion_FcsCore.dll`, loaded, ABI and structure sizes OK |
| second start | the cached file is reused (same bytes), loaded |
| kernels' self-test | passed 200 / 200 |
| coupled-cap solver's self-test | passed 50 / 50 |
| a loose `Ternion_FcsCore.dll` beside a plugin with none inside | loaded from beside the plugin |
| together with the Compass 0.3.1 and Su-30 0.4.1 one-DLL builds, and with the two-file Compass 0.3.0, in one process | each plugin loaded its own core from its own cache folder, all self-tests passed |
| no core anywhere | "not usable, the C# solve runs" with the reason; nothing else affected |
| coupled-cap solve for one sweep point (3 `Reserve` + 1 `PitchHeld`, 10 surfaces × 9 points, made-up data) | native 34 µs, C# on Mono 111 – 125 µs: **3.3 – 3.7×** |

As on the other jets, in the game the native code does the bench's float arithmetic, not Mono's (Mono carries C# floats in double). So in the game 0.3.0 and 0.2.3 differ in last digits and in some equal-cost allocator picks. To fly the C# code exactly as 0.2.3: `NativeAllocator = 0`.

## Timing
**Bench, the asymmetric-load sweep, 11 Nov 09:38 replay 182 – 236 s (heavy load), µs per tick, median of 3 runs, nothing else running:**
| build | mean | p95 | p99 |
|---|---|---|---|
| 0.2.2 | 368 | 732 | 1,467 |
| 0.2.3 (more solves: the negative side always) | 380 | 756 | 1,681 |
| 0.3.0, everything switched to C# | 391 | 775 | 1,539 |
| **0.3.0 default** | **330** | **529** | **639** |

- Same measurement on the 11:33 replay, 33 – 52 s: 0.2.2 432, 0.2.3 470, **0.3.0 327** µs mean.
- The bench is .NET 8, whose C# is already fast, and a 2-core VM. It probably understates the gain for the game, as it did on the F-22E (allocator 40 → 17 µs on the bench, 265 → 41 µs in the game).
- One odd reading, not explained: with the kernels native but the coupled check left in C# the bench sweep read 467 µs, slower than all-C#. I did not chase it (2-core VM, extra thread hand-offs are my guess, not measured).

**Game, estimate only.** Your 0.2.2 timer lines at 11:22 – 11:33: whole tick 1,160 – 1,470 µs, of it allocator 390 – 570, tables 108 – 116, asymmetric sweep 280 – 400. On the F-22E the same port took the allocator from 265 to 41 µs and the tables from 207 to 132. If the Ternion follows, the allocator alone gives back 300 – 450 µs per tick. The sweep's gain depends on how much of its time is the solver on Mono; the 10 s line will show it.

## Settings (Dev, "0. Debug"; all take effect at once)
| key | default | |
|---|---|---|
| `NativeAllocator` | 2 | 2 = native in the game; 1 = only where bit-identical (the bench; in the game = C#); 0 = C# as 0.2.3 |
| `NativeAllocatorCheck` | 0 | 2 = every solve and every coupled-cap LP call both ways (C# result used; counts in the log) |
| `NativeTables` / `NativeSweep` | on | the table rebuild / the sweep's capacity loop in the core |
| `NativeCoupledCap` | on | the coupled check's LP in the core |
| `SweepGridCache` | on | the sweep's geometry once per tick (C#) |
| `SlowTickLogUs` | 1500 | log any player tick slower than this with its 13 segments (0 = off) |

Bench-only switch: `CplGridCache` (the coupled check's points on cached geometry).

## The flight test (what gives the real numbers)
`TickTimerLog` on (default), `Telemetry` on, the heavy asymmetric load. Repeat a full pull, a pushover, a few full-stick rolls and reversals, ~30 s level. Hold each step ≥ 30 s (one clean 10 s window), then set it back:

| step | change | measures |
|---|---|---|
| 1 | all defaults | baseline |
| 2 | `NativeCoupledCap` off | Rust LP vs C# |
| 3 | `NativeSweep` off | Rust sweep kernel vs C# |
| 4 | `NativeTables` off | Rust tables vs C# |
| 5 | `NativeAllocator` 0 | everything in C# (= 0.2.3) |
| 6 | back to defaults | drift check |

Look for `Ternion FCS timing, whole tick in segments …` in `LogOutput.log`, and at start-up for `native allocator core in use … coupled-cap solver native`.

## Build notes
- Rust 1.97.1 (Ubuntu's package), Windows standard library compiled from source (`-Zbuild-std`), mingw-w64 linker; `native/build.sh` has the recipe. No crates downloaded. The tested core binary is `native/prebuilt/Ternion_FcsCore.dll` (SHA-256 `6caf95f3…`), which `build.sh` embeds.
- Source additions: `native/` (the crate with `src/cpl.rs`, `build.sh`, `README.md`, `prebuilt/`, `verify/`), `FcsNative.cs`, `bench/an/negload.sh` + `negload.py` (0.2.3).

## Install
- **One file:** `plugins\Ternion_FS3.FCS\Ternion_FS3_FCS.dll` 0.3.0 (1.39 MB: the core is inside) + `meta.json`. 0.2.3 kept as `Ternion_FS3_FCS.dll.0.2.3.bak`.
- **Dev DLL:** unchanged (no code). The new keys appear by themselves.
- **Mod manager:** update the version there, or its modpack may put the old DLL back.
- **Antivirus:** on first launch the plugin writes `Ternion_FcsCore.dll` into `BepInEx\cache` and loads it. If a scanner blocks that, the log says so and the FCS flies on its C# code.
