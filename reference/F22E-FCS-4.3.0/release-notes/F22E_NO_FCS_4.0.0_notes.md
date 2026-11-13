# F-22E Strike Raptor FCS 4.0.0: the allocator in Rust, used in the game only if you switch it on

Base: 3.2.1. Major bump at Tom's request. No control-law code changed.

**Changelog:** Added a native (Rust) build of the control allocator, about 8x faster per solve; the game keeps flying the C# allocator, exactly as 3.2.1, unless `NativeAllocator` is set to 2.

**Status:**
- Compiled against the game's assemblies.
- Benched: byte-identical to 3.2.1 with the native allocator on, off, and with every solve checked.
- Run on the game's own Mono runtime (the game's `mono-2.0-bdwgc.dll`, under Wine, with the JIT option Unity passes). That is the runtime, not the game: no Unity, no flight.
- **Not flown.**

## Tom (10 Nov)
- 02:11: "rewrite current f22 fcs 3.2.1 in rust where ever it is possible for better perf. Keep the exact same flight behavior."
- 02:31: "Bump version to 4.0.0"

## The finding that decides everything
**The game does not compute C# floats the way the bench does, so no compiled code can fly "exactly the same" as the C# in the game.**

- Unity starts the game's Mono with `-O=-float32`. In that mode every C# `float` expression is worked out in 64-bit (`double`) and rounded to 32-bit only when the value is stored to a field or an array, passed to or returned from a call, or kept across a branch.
  - Found as an unconditional call in `UnityPlayer.dll` (2022.3.62f2), and confirmed by running a test on the game's own Mono DLL: `(16777216f + 1f) - 16777216f` gives 1 with that option and 0 without.
- The bench runs .NET 8, where every float operation is rounded to 32-bit at once (strict IEEE arithmetic). So does Rust, and so does any compiled language.
- The difference is in the last digits of each number, but the allocator makes discrete picks (grid points, accept or reject a step, which plan), and a last-digit difference sometimes flips a pick.

**Measured: the same 26,700 allocator solves, same inputs, 3.2.1's C# on the game's Mono against the same C# on the bench** (six bench manoeuvres: corkscrew + push-over, pull + roll reversal, falling leaf without fins, 1 g reversals at 800 km/h, held 65° with a LERX and engine shot off, negative-AoA corkscrew):

| the two answers are | solves | share |
|---|---|---|
| identical to the bit | 1,895 | 7.1 % |
| different, every surface within 0.001° | 18,263 | 68.4 % |
| further apart, costs within 0.1 % (an equal-cost alternative) | 6,463 | 24.2 % |
| further apart, costs differing more | 79 | 0.3 % |

- Median difference where they differ: 0.0003°. 99th percentile: 0.09°. Over 0.1°: 186 solves (0.7 %). Largest: 50° on one surface for one solve.
- This is how far the bench's allocator has always been from the game's, for every version; it is not new in 4.0.0.
- The 50° case says the allocator's answer is not unique in places: two very different surface positions cost the same, and rounding noise picks between them. Not touched here; it may be worth a look for the surface-chatter cases.

**So the request has two halves that cannot both be met in the game:** a Rust allocator returns the bench's arithmetic, not Mono's. 4.0.0 therefore ships the Rust allocator and leaves the choice to you.

## What 4.0.0 does
**`F22E_FcsCore.dll`** (new, beside the plugin DLL): `Allocator.Solve` ported line for line to Rust — the reach pass, the two-horizon pick, `SolveCore`, the paired stab / nozzle search, the linear solve and `Effector.MAt`. Source in `native/fcs_core`.

**Dev setting `NativeAllocator`** ("0. Debug"):

| value | in the game | on the bench |
|---|---|---|
| 0 | C# allocator | C# allocator |
| **1 (default)** | **C# allocator: the same bits as 3.2.1, and the library is not even loaded** | native (there it is bit-identical) |
| 2 | native allocator | native |

- 1 means "use the library only where it returns exactly what the C# returns". The plugin tests the runtime's float mode at start-up and logs which case it is in.
- With the DLL missing, failing to load, or returning an error, the C# allocator runs.
- **`NativeAllocatorCheck`** (default 1): when the native allocator is in use, each allocator's first 64 solves and every 256th after are solved both ways and the C# result is used on those. 2 = every solve. The counts go in the tick timer's log line every 10 s as identical / within 0.001° / equal-cost alternative / different.

**The C# allocator is unchanged.** It is the reference and the fallback.

## What it buys (measured on the game's Mono, not in the game)
| | per solve |
|---|---|
| C# allocator, game's Mono | 137.5 µs |
| native, through the plugin's own call path, marshalling included | 16.4 µs |

- Ratio 8.4x. Machine: the 2-core VM, so read the ratio, not the microseconds.
- **In-game baseline** (your 9 Nov 21:35 log, 3.2.0, 10,289 ticks, timer columns): whole FCS tick 708 µs mean, of which main-thread allocator solves 194 µs.
- **Estimate with `NativeAllocator = 2`:** allocator about 23 µs, tick about 540 µs (−24 %). The estimator's six probe solves per tick, on the worker thread, drop by the same ratio. An estimate from the ratio; the timer columns will show the real figure once flown.

## Verification
**Bench (.NET 8), against 3.2.1:**

| run | per-case CSVs identical | suite tables | solves |
|---|---|---|---|
| default (native, sampled checks) | 119 / 119 | 5 / 5 | 413,966 native + 13,616 solved both ways, all identical |
| every solve checked | 119 / 119 | 5 / 5 | 427,582 solved both ways, 0 differences |
| `NativeAllocator = 0` | 119 / 119 | 5 / 5 | — |

**Game's Mono runtime, the shipped plugin DLL and the shipped native DLL, 26,700 recorded solves:**

| check | result |
|---|---|
| 4.0.0 as installed (`NativeAllocator = 1`) against the shipped 3.2.1 DLL | every output bit the same on all 26,700 |
| native (`= 2`) against the bench's C# result | 26,700 / 26,700 identical to the bit |
| native against the game's C# result (the plugin's own check, every solve) | the table at the top: 7.1 / 68.4 / 24.2 / 0.3 % |
| same C# with Mono's float32 option on | 26,700 / 26,700 identical to the bench |

The last row shows the float mode is the only cause of the game / bench difference.

## Not ported, and why
- **Table rebuild (87 µs), asymmetric-load sweep (147 µs), asym moment and slip probe (41 µs), laws (47 µs):** they call sin / cos / atan2 / exp through Unity's and Mono's own maths, which are statically linked inside the game's runtime (no maths DLL is imported), so a separate library cannot call the same functions. In the game they also hit the same float-mode wall as the allocator.
- **The rest (about 110 µs):** reading and writing the game's objects. That has to stay in C#.

## Your call
1. **Leave it at 1.** The game flies 3.2.1 exactly; 4.0.0 changes nothing in flight.
2. **Set `NativeAllocator = 2`** in the Dev cfg and fly it. Same law, the bench's arithmetic: what the bench verifies is then what flies, for the allocator. Tell me and I make 2 the default.
3. **Go further in Rust** (table rebuild, sweep, probes) under the same "bench-identical, not Mono-identical" standard. Larger port; about 275 µs of the tick is in those.
4. **Emulate Mono's rounding in the Rust code** to get the game's bits exactly. Possible in principle, and the replay harness on the game's Mono is the oracle for it. Not attempted: the rounding points depend on the JIT's internals, and every allocator change or Unity update would need it redone.

## Tools added to the source
- `native/build.sh`: builds the DLL (Rust 1.99.1, target `x86_64-pc-windows-gnu`, mingw-w64 linker).
- `native/verify/`: `host.c` runs a managed exe on the game's Mono the way Unity sets it up; `harness.cs` replays recorded solves through a plugin DLL; `record_patch.py` makes a test copy of the bench record them; `floatmode.cs` is the float-mode test. See `native/README.md`.
- `regen.sh` builds the Linux library for the bench; without Rust installed the bench runs the C# solve.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.0.0 + `F22E_FcsCore.dll` + `meta.json`. 3.2.1 is kept as `.dll.3.2.1.bak`.
- **Dev:** unchanged (the 3,584-byte DLL).
- **Release:** `C:\Games\NO mods\F22E-FCS-4.0.0-release`. The mod zip now holds two files, the plugin DLL and `F22E_FcsCore.dll`.
- **Mod manager:** update the version there, or its modpack may put an old DLL back.
