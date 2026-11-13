# FS-12 Revoker FCS 1.0.0: the FCS on several cores

Base: 0.8.3. Tom (9 Nov): "port it to every one except the flanker"; numbering at his request ("bump revoker and ifrit to 1.0.0").

**Status:**
- Compiled against the game's assemblies.
- Benched byte-identical to 0.8.3, with every switch on and with every switch off.
- **Not flown.** 0.8.3 is kept as `.dll.0.8.3.bak`.

## What changed
The F-22E's performance work (3.0.4–3.1.3), ported. Same code as the F-22E's, adapted to this jet. No control-law change.

**On other cores** (the shared .NET thread pool, at most a quarter of the logical cores per jet, 1–3 helpers; the main thread always takes part, and a job no helper has started is run by the main thread, so a busy pool can delay the speed-up but never stalls a tick):
- **Capacity estimator** (`EstimatorThreaded`): its 8 allocator probe solves run on a worker on copies of the effectors and the allocator, collected at the top of the next tick. The laws already read the previous tick's estimate, so they see the same numbers.
- **Table rebuild** (`ParallelTables`): each surface's moment table. The game's objects are read on the main thread first.
- **Allocator reach plan** (`ParallelAllocator`): the third solve of the two-horizon pick, on a copy, while the main thread does the other two.
- **Asymmetric-load sweep** (`ParallelSweep`): the AoA points of the sweep.
- **Lift envelope and hybrid slopes** (`ParallelEnvelope`): the 25 envelope points and the 4 slope evaluations, every 0.1 s.

**Less work on the main thread:**
- **Game reads once per tick** for the sweep and the envelope, instead of once per part per point.
- **Camber base cached:** the host's undeflected camber force is worked out once per table rebuild instead of for every one of the ~31 table points.
- **CSV written by a background thread:** the main thread only boxes the values; the text is formatted and written off the main thread. The estimator's columns are filled at the next tick, after its job is collected.

**Timers:** with `TickTimerLog` on, every 10 s `LogOutput.log` gets:
- the player jet's FCS time per tick (mean / median / p95 / max) and its split by phase;
- all AI jets together;
- the airframe force hooks (drag relief, camber, downwash), which run outside the FCS tick.

The CSV gains 17 timer columns after the fixed block (`fcsUs` … `tVmcUs`).

**Ejection:** once the pilot ejects, the FCS disengages and the stock fly-by-wire takes over (the F-22E 3.1.0 fix). Before, the empty jet looked like an AI jet; with `AIFlightControl` on, the FCS kept flying it.

All six Dev switches ("0. Debug") default on and take effect at once, for an A/B mid-flight.

## What was specific to this jet
- **The allocator's own per-tick settings.** This jet's allocator has three settings the F-22E's doesn't: the velocity-frame lateral weighting (slip frame on, sin a, cos a). The worker copies (estimator, reach plan) take them; this gap was found on the Ifrit port first, where the bench showed the estimator's columns differing without them.
- **The hinged elevons / flaps (`HingeFlapEffector`) in the sweep.** Their EvalFlow reads the game's transforms and rigidbodies, which can't be done off the main thread. Those reads now happen once per sweep tick on the main thread (`Snap`), and the sweep's points use them (`EvalFlowSnap`: the same expressions on the same values). The elevon-authority loops over every grid point now use the per-flow preparation too.
- **The visual-hinge rotation** (`AirframeRot`) is used in every gather, as in the original loops.
- **PassiveStep:** the method was still in the source but never called. Removed (dead code).

## Bench identity (0.8.3 vs 1.0.0)
| check | result |
|---|---|
| Suite per-case files (reg / sw / cork / dmg), all switches on | **159 / 159 identical** |
| Same, all switches off | **159 / 159 identical** |
| Suite summaries | identical (only the tag echo differs) |
| Replays of the 7 Nov 10:23 log (4 windows of 6 s: max AoA, max roll rate, min AoA, mid-flight) | **4 / 4 identical** |
| 12 s pull / push / roll-reversal case: the bench log, threaded / single-thread | identical / identical |
| Same case, the game CSV through the real telemetry path | identical (timer columns aside; the last row is written at dispose, which the bench does not run) |
| Clone completeness (reflection: every Effector field, every allocator field) | the base Effector fields and every allocator setting match; the rest are scratch, counters or subclass fields the solves do not read |
| All four ports at once on 2 cores, estimator job left running into the next tick (as in game), 2 runs each | game CSV identical every time, no deadlock |

## Bench timing (main thread, µs per tick, same case, 2-core VM, ±15 %)
| | mean µs per tick (3 runs) |
|---|---|
| 0.8.3 | 2209–2405 |
| **1.0.0, all on** | **887–1031** |
| 1.0.0, all off | 2127–2436 |

About −58 % on this VM. The bench collects the estimator right after the tick, outside the timer, so the worker gets the gap between ticks, as it does in game during rendering.

## Not checked
- Not flown. The game's own timer will show the real numbers.
- Several mods' jets in one game process at once (the bench has one plant per process): stood in for by the four-jet run above.

## Install
- **Main:** `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` 1.0.0 + `meta.json`; 0.8.3 kept as `.dll.0.8.3.bak`.
- **Dev:** `plugins\Revoker_FS12_FCS_Dev.dll` 1.0.0; the old one kept as `.bak`.
- **Release:** `C:\Games\NO mods\Revoker-FCS-1.0.0-release` (player zip, source zip with the bench, optional dev zip, notes, meta.json, DLL).
