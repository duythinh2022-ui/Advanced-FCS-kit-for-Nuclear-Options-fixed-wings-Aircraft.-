# F-22E Strike Raptor FCS 4.1.0: Rust on by default; the sweep's loop and the tables' maths in Rust

Base: 4.0.1. No control-law code changed.

**Changelog:** The native (Rust) core is on by default (`NativeAllocator` 2, `NativeAllocatorCheck` 0) and now also runs the asymmetric-load sweep's inner loop and the surface tables' per-deflection maths.

**Status:**
- Compiled against the game's assemblies.
- Benched: byte-identical to 4.0.1, 4.0.0 and 3.2.1 with everything native, with the new kernels off, and all in C#.
- Run on the game's own Mono runtime (under Wine): the plugin and the new DLL load, the kernels' self-test passes, the allocator is unchanged, and the kernel was timed against its C# twin. That is the runtime, not the game.
- **Not flown.** The in-game saving below is an estimate; my last two were optimistic.

## Tom (10 Nov)
- 16:54: "Not thing can be ported to rust anymore right?"
- 17:06: "Set always rust allocator on by default and check 0 by default. Do rust for what rest can be done like u said"

## What 4.1.0 changes
**1. Defaults.**

| Dev setting ("0. Debug") | 4.0.x | 4.1.0 |
|---|---|---|
| `NativeAllocator` | 1 (C# in the game) | **2** (Rust in the game) |
| `NativeAllocatorCheck` | 1 (every 256th solve both ways, C# answer used) | **0** (every solve is Rust) |

- Forced once onto an existing Dev file (`AirframeRevision` 28), as with earlier default changes. After that the file's values are yours again.
- Players without the Dev DLL get 2 / 0 from the built-in defaults.
- With `F22E_FcsCore.dll` missing, the wrong version, or failing its checks, the C# code runs.

**2. The grid kernels** (`f22e_fcs_grid` in `F22E_FcsCore.dll`): one surface's moment at every deflection of its grid in one airflow.

| user | what moved to Rust | what stays in C# |
|---|---|---|
| asymmetric-load sweep (`NativeSweep`, default on; needs `SweepGridCache`) | the capacity loop: per deflection one rotation, one `atan2`, the airfoil table, the force and its moment (the surface and its fin / wing section), then roll / yaw acceleration and the min / max kept | the rotations (`GridGeom`), the per-point flow terms, the parts loop, the cap logic |
| table rebuild (`NativeTables`, default on) | the same per-deflection maths for the 26 – 31 grid points of each surface's table | the rotations, the live point, the table's post-processing, reading the game's objects |

- The rotations need sin / cos, so they stay in C#. With the tables native they are worked out once per tick in the rebuild and the sweep reuses them (its own geometry pass is skipped).
- `atan2` is written as C# writes it, `(float)Math.Atan2((double)y, (double)x)`. On the bench both sides call the same C library, so the result is identical; that was the open question at 16:54, and it holds.
- A generic airfoil (no charts) and a surface with no airflow stay on the C# loop. A NaN flow makes the kernel decline and the C# loop then throws exactly as before.

**3. Self-test at load.** The kernels run on made-up geometry and airfoil tables and are compared with a C# twin (`FcsNative.GridTwin`). A mismatch, a wrong structure size or an error return switches the kernels off for the session (one warning in the log) and leaves the native solve on.

**4. Log.** The start-up line and the 10 s native line now end with the kernels' state and call counts, e.g. `grid kernels: sweep native, tables native: 123456 kernel calls, 0 left to C#`.

- ABI 4000001 → 4001000: 4.1.0's plugin needs 4.1.0's `F22E_FcsCore.dll` (a 4.0.x one is refused and C# runs).
- Files: `native/fcs_core/src/lib.rs`, `FcsNative.cs`, `SurfaceEffector.cs`, `AirfoilTable.cs`, `FcsController.cs`, `Plugin.cs`, `TickStats.cs`.

## Verification
**Bench suite (.NET 8), against 4.0.1 / 4.0.0 and 3.2.1:**

| run | per-case files identical | suite tables | native work |
|---|---|---|---|
| default: solve and both kernels native | 153 / 153 | 5 / 5 | 427,582 solves, 1,068,812 kernel calls, 0 left to C# |
| `NativeSweep` and `NativeTables` off | 153 / 153 | 5 / 5 | 427,582 solves, 0 kernel calls |
| `NativeAllocator = 0` (all C#) | 153 / 153 | 5 / 5 | none |

- Sensitive to each kernel: with the kernel's output scaled by 1.000001 in a test build, a corkscrew case's CSV changed with only the sweep kernel on (333,437 bytes) and with only the table kernel on (364,980 bytes), and not with both off. Test build only, removed.
- Flight-log replays (the 16:48 flight's held roll, 63.7 s pull, slow roll): byte-identical to 4.0.1's.

**Game's Mono runtime (Wine), the shipped plugin DLL and the shipped native DLL:**

| check | result |
|---|---|
| C# solve (`NativeAllocator = 1`), 26,700 recorded solves | every output bit equal to 4.0.0 and 3.2.1 |
| native solve against the bench's C# | 26,700 / 26,700 identical |
| native against the game's C#, every solve | 1,895 identical / 18,263 within 0.001° / 6,463 equal-cost / 79 different (as 4.0.0) |
| kernels' self-test | passes |
| kernel against its C# twin, game's float mode | 19 % of 5,952 moment components bit-identical, the rest last-digit differences (largest 2.5e-4 on a component near zero) |
| kernel against its C# twin, Mono's float32 on | 5,952 / 5,952 bit-identical |

## Timing
**Bench (.NET 8, 2-core VM, 34 s flight, median of 3, µs per tick):**

| | tick | tables | allocator | sweep |
|---|---|---|---|---|
| all C#, 4.0.0's sweep loop | 381 | 84 | 40 | 171 |
| all C# (4.0.1) | 302 | 87 | 40 | 86 |
| Rust allocator only (4.0.1 at `NativeAllocator = 2`) | 272 | 82 | 17 | 85 |
| **4.1.0 default** | **178** | **54** | **15** | **33** |

**Game's Mono, the kernel alone** (one surface, 31 deflections, with its host section): native 3.3 – 3.7 µs, C# twin 15.7 – 17.1 µs, **4.6 – 4.7x**.

**Game, estimate only.** 4.0.1 with Rust measured 581 µs per tick (sweep 127, tables 105). The kernels replace the flow's part of both; the rotations and the game-object reads stay. My guess is sweep 60 – 80 and tables 80 – 95, tick 500 – 540 µs. Read `asym sweep` and `tables` in the 10 s timing line.

## What is left in C#, and why
- The control laws (53 µs): porting them means keeping the whole FCS in two languages.
- Reading and writing the game's objects, the asymmetric moment, mass properties, flaps, CSV (about 190 µs): Unity's API.
- The surfaces' rotations (inside tables and sweep): `Quaternion.AngleAxis` / `Inverse` are Unity's own native code.

## Still open (not touched)
- Held full-stick roll near 30° AoA: aileron commands alternate about ±3° tick to tick (bench-reproducible, replay `178.6 185.0` of the 16:48 log).
- Pitch-rate overshoot on a full pull started during a fast roll (parked by Tom: the AoA limit holds).

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.1.0 + **`F22E_FcsCore.dll` 4.1.0 (new file, both are needed)** + `meta.json`. 4.0.1's pair is kept as `Aryx_F22E_StrikeRaptor_FCS.dll.4.0.1.bak` and `F22E_FcsCore.dll.4.0.1.bak`.
- **Dev:** unchanged (the 3,584-byte DLL). On first launch the Dev file gets `NativeAllocator = 2`, `NativeAllocatorCheck = 0`.
- **Release:** `C:\Games\NO mods\F22E-FCS-4.1.0-release`. The mod zip holds the plugin DLL, `F22E_FcsCore.dll` and `meta.json`.
- **Mod manager:** update the version there, or its modpack may put an old DLL back.
