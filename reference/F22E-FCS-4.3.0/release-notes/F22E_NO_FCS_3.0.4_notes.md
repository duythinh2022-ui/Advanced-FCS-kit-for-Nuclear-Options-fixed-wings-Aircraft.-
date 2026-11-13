# F-22E FCS 3.0.4: the capacity estimator on a worker thread, the CSV written off the main thread, and a tick timer

Base: 3.0.3 (rebuilt here byte-identical to the released DLL, md5 `5905ab12…`). Patch bump: no behaviour change.

**Status:**
- Compiled against the game's assemblies.
- Benched byte-identical to 3.0.3: suite, replays, game CSV.
- **Not flown.** 3.0.3 is kept as `.dll.3.0.3.bak`.

## Tom (5 Nov, 22:09)
"can u start working on making the fcs mods multi-threaded" / "start with raptor first". This is plan §3/§4 of `claude/FCS_performance_handoff.md`.

## What runs where now
| part | 3.0.3 | 3.0.4 |
|---|---|---|
| estimator probes (`CapacityEstimator.Step`: 6 allocator re-solves) | main thread | **worker** (thread pool, one job per jet per tick) |
| CSV text formatting + file writes | main thread | **writer thread** (player jet only) |
| table rebuild, the real solves, the laws, `PassiveStep` | main thread | main thread (needed this tick; `PassiveStep` reads live rigidbodies) |

## Why the flying is identical (by construction)
- **The laws already read the previous tick's estimate.** Every reader of `capEst` (PitchLaw, LateralLaw) runs at Tick ~4737. The estimator ran later, at ~5336.
- **The new order:**
  - The job is kicked at that same spot in tick N.
  - It is joined at the very top of tick N+1, before anything reads `capEst`.
  - If it hasn't finished, the main thread waits: that tick is slower, never different.
- **The worker owns copies of everything it touches.** The probes call `Allocator.Solve` and write effector commands, so the worker gets:
  - one clone per effector (same concrete type, own `grid`/`M` tables, `partner` re-pointed to the clone);
  - its own `Allocator` (`CopyConfigFrom`);
  - a copy of `IaInv`.

  All of these are refreshed by value at each kick.
- **The one side effect on the main allocator was its `reachMoves` counter**, which the CSV's reach column reads. That column now adds the worker allocator's count back in. `resDbg`/`lastCost` are read before the estimator (`allocResMain`) or not at all.
- **Exceptions:** a worker exception is re-thrown at the join, so `Registry.Fail` handles it as before, one tick later.

## The CSV
- **CaptureRow (end of tick N, main thread):** `WriteRow` is split. CaptureRow boxes every value with 3.0.3's exact format strings, in the same order.
- **FinishRow (top of tick N+1, after the join):** fills the estimator's columns and the reach count.
- **The writer thread** formats and writes, and flushes about once a second.
- **3 new columns at the end of the block, before the per-surface columns:**
  - `fcsUs`: that tick's Tick + VmcApply wall time, in µs.
  - `estWaitUs`: how long tick N+1 waited for tick N's job.
  - `estThr`: 1 when that tick's estimator ran threaded.
- The last row is written when the controller is disposed.

## New Dev settings ("0. Debug")
- `EstimatorThreaded` (default **true**). false = the 3.0.3 single-thread path. Takes effect at once, so it can be A/B'd mid-flight.
- `TickTimerLog` (default **true**). Every 10 s it writes to `LogOutput.log`:
  - one line for the player's jet: mean / median / p95 / max µs per tick, and how many joins waited;
  - one line for all AI F-22Es together: the per-jet mean, and the sum of the jets' means = the host's cost per physics tick.

## Bench identity (3.0.3 vs 3.0.4 threaded, and 3.0.4 with EstimatorThreaded=false)
| check | result |
|---|---|
| Suite: rev / cork / reg / util / dmg summaries | identical (only the header line's argument echo differs) |
| Suite per-case output files (153) | **byte-identical**, threaded and sync |
| Replays: 10 released + 10 held + 25 other pushes from the leaf-case logs, 7 free windows from the 5 Nov 3.0.2/3.0.3 logs (stab shot off, no-fin straight flight, roll creep, departures) | **52 / 52 byte-identical** |
| 40 s corkscrew / push-over case, bench CSV (all estimator columns) | same md5 in all three |
| Same case, the game CSV (`WriteHeader`/rows through the real code) | 2316 / 2316 rows identical; new columns aside |
| Same, with the bench's early join off (worker overlapping the plant step, as in game) | game CSV and every non-estimator bench column identical |
| Clone completeness (reflection: every `Effector` field, every non-scratch `Allocator` field, at 77 + 25 points; clean jet, and fins + right flaperon/aileron shot off mid-flight) | 0 mismatches |

## Bench timing (main-thread Tick + VmcApply, µs; 2-core 2.8 GHz VM, .NET 8; 3 runs each, ±15 % run to run)
| case | 3.0.3 median / mean / p95 | **3.0.4 threaded** | 3.0.4 sync |
|---|---|---|---|
| corkscrews and push-overs, telemetry off (≈ an AI jet) | 670–695 / 680–725 / 1085–1281 | **471–505 / 473–504 / 805–1044** | 662–724 / 716–724 / 1142–1278 |
| same, game CSV on (in the bench the rows are still formatted on the main thread) | 804–873 / 880–1012 / 1760–2421 | 592–629 / 668–677 / 1481–1559 | 729–806 / 845–960 / 1543–2161 |

- **About −29 % median and −31 % mean main-thread time per jet.** The probes were ~200 µs of the tick here, not the handoff's ~450 µs estimate.
- **What remains on the main thread (~480 µs on this VM):** the table rebuild, the real solves and the laws.
- **How the timing was measured:** these runs join right after the tick (outside the timer), i.e. the worker gets the gap between ticks, as it does in game during rendering.
- **When the worker has no time:** the bench's own plant step is only ~0.3 ms. With the join at the next tick, the main thread waited on 88 % of ticks (mean 292 µs), and on 2 cores the total was no better than 3.0.3.
- **In game, the same applies when Unity runs two physics ticks back to back** (frame rate under the 60 Hz physics rate): the second one waits. The in-game timer will show how often: `estWaitUs`, and "joins waited" in the log.

## Not checked
- Not flown. The game numbers come only from the timer: fly one log with `EstimatorThreaded` on, then toggle it off mid-flight for comparison.
- Several AI F-22Es at once (per-jet jobs in parallel) is not benched: the bench has one plant.
- The writer thread and the Dispose-time flush are game-only (the bench stub writes rows synchronously).

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 `760fceb41427773bca48687468fb9c1c`) + `meta.json` 3.0.4; 3.0.3 kept as `.dll.3.0.3.bak`.
- **Dev unlock:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 3.0.4 (md5 `ad2533ec0127007e292efbfa3f54993e`); 3.0.3 kept as `.bak`.
  - The Dev DLL is compiled from `DevUnlock/FcsDevPlugin.cs` directly. `build.sh` excludes `*/DevUnlock/*`, so `build.sh DevUnlock` produces an empty 3584-byte assembly. The direct build reproduces 3.0.3's Dev DLL byte for byte.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.0.4-release`. The mod zip holds the main DLL only (the Dev DLL ships separately as `(Optional)Aryx_F22E_StrikeRaptor_FCS_Dev.zip`). The source zip includes the bench (with `GAMECSV=<path>` for the game-CSV dump and `JoinAfterTick`).

## Next
- The same port to the Ifrit, Revoker and Compass: `EstimatorJob.cs`, `TelemetryRow.cs`, the Effector/Allocator copy methods, the Tick join and the timer.
- Each one is verified the same way, byte-identical on its own bench.
