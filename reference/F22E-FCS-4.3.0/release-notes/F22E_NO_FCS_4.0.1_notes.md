# F-22E Strike Raptor FCS 4.0.1: the asymmetric-load sweep at about half its cost, same numbers

Base: 4.0.0. No control-law code changed.

**Changelog:** The asymmetric-load sweep works out each surface deflection's geometry once per sweep tick instead of once per AoA point (about half the sweep's time on the bench, results unchanged); `NativeAllocator` and `NativeAllocatorCheck` are a 0 / 1 / 2 choice in Configuration Manager.

**Status:**
- Compiled against the game's assemblies.
- Benched: byte-identical to 4.0.0 and to 3.2.1, with the new loop on and off.
- Plugin DLL run on the game's own Mono runtime (under Wine): it loads, and the allocator returns the same bits as 4.0.0 on the 26,700 recorded solves. The sweep is not exercised there (it needs the game's aircraft objects).
- **Not flown.** The in-game saving is an estimate until the timer columns show it.

## Tom (10 Nov)
- 16:07: "what can be done more"
- 16:11: "but the aoa limit still holds so i dont think pitch rate overshoots is worth to fix at least for now. Do the asymmetric load sweep thing"

## Why the sweep cannot be skipped or deferred
At 16:07 I offered two ways to cut the sweep, both unchecked: skip it while the jet is symmetric, or stop the main thread waiting for it. **Neither survives the code.**

- **What the sweep is:** 22 AoA points, 3 per tick, a new cycle every 0.2 s. In the 15:52 log it ran on 61 % of ticks, about 300 µs on a sweep tick, 199 µs per tick on average (31 % of the tick with the Rust allocator).
- **It is not only the asymmetric-load cap.** Each point also writes, for its AoA, the airframe's lateral capacity (`asymCr`, `asymCy`), the load's roll and yaw demand (`asymAr`, `asymAy`) and the net nose-down / nose-up authority (`asymNetDn`, `asymNetUp`, `authNetDn`). The laws read those every tick on a symmetric jet too: the coupling roll limit, the authority rate limit, the AoA-capture thrust brake, the damage lateral limit, the shared lateral budget, fuel trim. Skipping the sweep would freeze them: a behaviour change.
- **The join cannot move.** The tick's points are written and then read (interpolated at the current AoA) a few lines later in the same tick, so the points have to be finished there.

So the only cut that keeps the numbers is making each point cheaper.

## What 4.0.1 changes
**The capacity loop.** For each AoA point the sweep evaluates every grid deflection of every surface in that point's airflow, to find the roll, yaw and pitch range the surface has there: 2 stabs x 31 + 2 rudders x 31 + 4 flaperons x 26 = **228 evaluations per point, 684 per sweep tick**. Each one rebuilt everything from scratch (`SurfaceEffector.EvalFlow`).

| part of one evaluation | depends on | 4.0.0 | 4.0.1 |
|---|---|---|---|
| the surface's rotation at the deflection, its inverse, span axis, moment arm; the host fin / wing section's cambered rotation, its inverse and span axis (up to 2 `AngleAxis`, 3 quaternion products, 2 inverses, 3 rotations) | the deflection only | 684 x per sweep tick | 228 x per sweep tick (`GridGeom`, the surfaces in parallel) |
| flow speed, the stabs' downwash turn, dynamic pressures, the host's arm and undeflected force | the airflow only | 684 x | once per surface and point (`PrepFlow`, since 3.1.3 for the other calls) |
| local AoA, airfoil lookup, force, moment (`EvalFlowGrid`) | both | 684 x | 684 x |

- Same expressions in the same order; the values are only worked out fewer times.
- New Dev setting **`SweepGridCache`** ("0. Debug", default on). Off = the 4.0.0 loop.
- Files: `SurfaceEffector.cs` (`GridGeom`, `EvalFlowGrid`), `Camber.cs` (`FoilPre`), `FcsController.cs` (`SwGridGeom`, one line in `AsymSweepPoint`), `Plugin.cs`.

**Settings fix (my bug in 4.0.0).** `NativeAllocator` and `NativeAllocatorCheck` had no allowed-value list, so Configuration Manager drew a free text box, and emptying it while typing logged `Failed to draw setting NativeAllocator - System.FormatException` (three times in the 15:52 log). They now carry `AcceptableValueList<int>(0, 1, 2)`: a dropdown. Defaults unchanged (1 and 1).

## Verification
**Bench suite (.NET 8), against 4.0.0 and 3.2.1:**

| run | per-case files identical | suite tables identical |
|---|---|---|
| 4.0.1, `SweepGridCache` on (default) vs 4.0.0 | 153 / 153 (119 CSVs + 34 damage logs) | 5 / 5 |
| 4.0.1, `SweepGridCache` off vs 4.0.0 | 153 / 153 | 5 / 5 |
| 4.0.1 on vs 3.2.1 | 153 / 153 | 5 / 5 |

- The comparison is sensitive to the new code: a deliberate 1e-6 change inside `EvalFlowGrid` changed a corkscrew case's CSV (320,242 bytes), and switching `SweepGridCache` off made that perturbed build identical again. Test build only, removed.

**Game's Mono runtime, the 4.0.1 plugin DLL:** `NativeAllocator = 1`: every output bit of 26,700 recorded allocator solves equal to 4.0.0 (and so to 3.2.1). `NativeAllocator = 2`, every solve compared: 1,895 identical / 18,263 within 0.001° / 6,463 equal-cost / 79 different, the same counts as 4.0.0.

**In the game, last digits:** the game's Mono carries some intermediate float values at higher precision than the bench (the 4.0.0 finding), and the new loop stores a few values the old one recomputed in place. With `SweepGridCache` on, the sweep's numbers in the game can therefore differ from 4.0.0's in the last digit. Not measured. This is the same kind of difference as the 3.1.0 – 3.1.3 restructurings, and nothing like the allocator's discrete picks: the sweep is continuous maths.

## Timing
**Bench (.NET 8 on the 2-core VM, one 34 s flight, 2,040 ticks, median of 3 runs, µs per tick):**

| | sweep, 4.0.0 loop | sweep, 4.0.1 | whole tick |
|---|---|---|---|
| sweep on one thread | 211 | 98 (−54 %) | 418 → 301 |
| sweep on the pool (2 cores) | 184 | 95 (−48 %) | 391 → 312 |

**Game, estimate only:** the sweep from about 199 to about 100 µs per tick. Tick with the Rust allocator about 638 → 540 µs, with the C# allocator about 873 → 775. Read `asym sweep` in the 10 s timing line, or `tSweepUs` in the CSV, for the real figure.

## Not done
- The pitch-rate overshoots (Tom: not worth fixing for now, the AoA limit holds).
- `NativeAllocator` default stays 1.
- A native (Rust) version of the capacity loop. With the rotations now worked out in C#, the loop that is left needs only `atan2` and table lookups, so it could go into `F22E_FcsCore.dll` under the same "bit-identical on the bench" rule as the allocator. Likely the sweep at 30 – 40 µs with `NativeAllocator = 2`. Not built.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.0.1 + `meta.json`. `F22E_FcsCore.dll` is unchanged (same file as 4.0.0). 4.0.0 is kept as `.dll.4.0.0.bak`.
- **Dev:** unchanged (the 3,584-byte DLL).
- **Release:** `C:\Games\NO mods\F22E-FCS-4.0.1-release`.
- **Mod manager:** update the version there, or its modpack may put an old DLL back.
