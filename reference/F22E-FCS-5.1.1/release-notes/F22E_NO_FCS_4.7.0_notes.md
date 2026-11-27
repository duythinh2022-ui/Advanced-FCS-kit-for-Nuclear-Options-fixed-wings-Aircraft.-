# F-22E Strike Raptor FCS 4.7.0: the roll-rate cap reads the yaw capacity from the hold allocation

Base: 4.6.7. Same change as the Revoker FCS 1.7.0, so the two jets stay on one law. The Su-30 FCS (0.6.6) still has the 4.6.7 half-span read.

**Status:** compiled, bench replays of the 23 Nov 21:09 and 24 Nov 00:42 flight logs, held rolls and the full suite A/B run (below). The new DLL has **not been loaded in the game and not flown**.

Tom (24 Nov): "claude/FCS_surface_rate_saturation_findings_2026-11-24.md builfd for revoker and raptor first, find a better, cleaner way if u can".

## Bottom line for this jet
**On the bench this build is neutral on the F-22E. It does not fix the F-22E's surface rate saturation.** It replaces 4.6.7's half-span read with the cleaner one and puts the cap's reading back at the 4.6.6 law's level. The Revoker's saturation came from a loop through the governor (see the Revoker 1.7.0 notes); on the F-22E that loop is nearly idle, so opening it changes little.

## Change (the same code shape as the Revoker 1.7.0)
- The capacity estimator gets one more read, **the reversal read** (`capRevP` / `capRevN`), and the roll-rate cap (the reversal model behind `RollPaceRateBlend`) is its only consumer.
- It starts from the **hold allocation** instead of this tick's command: the surface positions that would make zero angular acceleration at the present rates (the allocator's answer to `hold = mNowF - I*wdotF`). It moves with the flight condition, not with the rate loops. One solve a tick, warm-started from its own last answer; its four probes continue from their own last answers.
- A side's figure is what the allocator reaches from the hold allocation, and never less than what the yaw axis is already doing toward that side.
- Same composite as `capZeP/N` (coordinated-rotation read and yaw-only read, weighed by the rudder weight).
- **The pitch/roll trade, the push governor and the pace read this tick's command exactly as in 4.6.6.**
- The 4.6.7 half-span read and its key `RollRateCapSpan` are gone.
- New dev key **`6. Lateral governor / RateCapFromHold`** (true). false = the 4.6.6 law exactly, switchable in flight.
- Four new log columns after `ceWdNs`: `ceRevP`, `ceRevN`, `ceHoldZ`, `ceHoldX`.
- The native core (`F22E_FcsCore.dll`) is unchanged.

### Why only the rate cap
The first build moved every estimator consumer to the hold read. On this jet that regressed the suite: pull-then-roll 250 km/h AoA overshoot 45.6 -> 54.1 deg and sideslip 1.2 -> 3.2; push+roll 200 2.1 -> 4.0; push-then-pullthrough+roll 450 5.5 -> 9.8. A bench build with both reads and a per-consumer switch, all 16 combinations: the trade on the hold read is what regressed (it divides this tick's authority, so it needs this tick's command), the push governor added sideslip in the push cases, the pace changed nothing, and the rate cap alone was neutral to slightly better (regression-set sideslip summed over 18 cases: 58.0 with the 4.6.6 law, 59.2 with 4.6.7, 56.7).

## Why it is neutral here
Held loaded rolls on the bench, 3.6-11.5 s. `rcAOpp` is the reversal capacity the cap reads (deg/s^2).

| Case | Law | `rcAOpp` mean | Slow swing (0.5-5 Hz, rms) | Tick-to-tick jump (mean) | Roll target tick-to-tick (deg/s) |
|---|---|---|---|---|---|
| 460 km/h, 0.45 aft, TVC on | 4.6.6 law | 121.0 | 2.38 | 0.90 | 0.09 |
| | 4.6.7 | 103.7 | 0.89 | 0.46 | 0.08 |
| | 4.7.0 | 130.3 | 0.73 | 0.13 | 0.09 |
| 550 km/h, 0.6 aft, TVC on | 4.6.6 law | 108.7 | 3.27 | 1.09 | 0.15 |
| | 4.6.7 | 93.7 | 1.82 | 0.87 | 0.15 |
| | 4.7.0 | 118.7 | 1.33 | 0.34 | 0.14 |
| 300 km/h, 0.45 aft, TVC on | 4.6.6 law | 62.8 | 1.42 | 0.39 | 0.07 |
| | 4.6.7 | 53.8 | 0.37 | 0.21 | 0.06 |
| | 4.7.0 | 65.7 | 0.45 | 0.15 | 0.06 |

The reading is steadier in 4.7.0 and sits at the 4.6.6 level again (4.6.7 read 10-15 % lower). But the reading only moved about 1 % from tick to tick on this jet to begin with, and the roll target did not follow it (0.06-0.15 deg/s per tick under all three laws; the Revoker's cap jumped 1.5 deg/s per tick). So nothing downstream changes.

## Bench: replays of the flight logs (TVC as flown)
Mean surface travel in deg/s (stabilators, rudders, aileron, flaperon). 21:09 log unless noted.

| Window (s) | Game | 4.6.6 law | 4.6.7 | 4.7.0 | At limit: game / 4.7.0 |
|---|---|---|---|---|---|
| 80.8-84.6 | 44.1 | 37.9 | 37.0 | 36.8 | 25.9 / 21.3 % |
| 50.5-54.0 | 46.3 | 34.9 | 33.8 | 33.3 | 28.8 / 17.4 % |
| 20.7-24.4 | 31.2 | 27.2 | 25.6 | 26.7 | 14.7 / 8.5 % |
| 31.6-38.2 | 36.4 | 36.1 | 35.2 | 35.9 | 17.0 / 16.9 % |
| 114.4-117.6 | 47.1 | 39.0 | 39.2 | 39.1 | 27.5 / 17.4 % |
| 68.7-72.4 (00:42 log) | 31.8 | 31.6 | 31.0 | 30.7 | 11.6 / 9.4 % |

The bench reproduces the game's surface activity here (0.7-1.0x), and the three laws are within 1.6 deg/s of each other. The saturation in these windows is not the rate-cap loop. The replay analysis points at the allocator re-dividing the roll between surfaces as AoA crosses their staggered stall points during the manoeuvre; that is an analysis, not yet confirmed by a fix.

## Bench: held loaded rolls
Travel in deg/s, 4.6.6 law / 4.6.7 / 4.7.0. Roll rate and sideslip unchanged within 0.8 deg/s and 0.2 deg.

| Case | Travel |
|---|---|
| 300 km/h, 0.45 aft, TVC on | 9.2 / 8.8 / 8.2 |
| 460 km/h, 0.45 aft, TVC on | 5.7 / 5.2 / 4.7 |
| 300 km/h, 0.45 aft, TVC off | 14.5 / 14.5 / 14.0 |
| 460 km/h, 0.45 aft, TVC off | 6.9 / 6.7 / 6.3 |
| 400 km/h, full aft, TVC on | 17.7 / 17.7 / 17.7 |
| 400 km/h, full aft, TVC off | 23.0 / 22.4 / 22.5 |
| 550 km/h, 0.6 aft, TVC on | 17.0 / 17.0 / 16.5 |

## Bench: full suite, 4.6.6 law / 4.6.7 / 4.7.0
Within the spread between the 4.6.6 law and 4.6.7 except where listed.

- roll-then-pull-rev 400: sideslip 5.2 / 5.6 / 4.2.
- pull-then-pushover+roll 450: peak roll 90 / 85 / 78 deg/s, sideslip 5.6 / 5.2 / 5.0.
- pull-then-roll 250 / 320 / 400: 1.3 / 1.2 / 1.3, 3.4 / 3.4 / 3.5, 3.7 / 3.6 / 3.8; AoA overshoot unchanged (45.6).
- push+roll 200: 2.0 / 2.1 / 2.2. push-then-pullthrough+roll 450: 4.9 / 5.5 / 5.3.
- pull + roll reversal at 0.7 aft, 556 km/h: the t90 figure reads 1.13 / 2.32 / 2.17 s, but the three roll-rate curves are within 11 deg/s of each other for 0.3 s (the new rate sits on the 90 % threshold); peak sideslip 18.4 / 18.4 / 17.9.
- Corkscrew entries (20): early peak, dip and mean roll inside the 4.6.6-4.6.7 spread or within 6 deg/s of it, except 850 km/h full aft full thrust early peak 230 / 206 / 239 and 700 km/h 0.7 aft roll at 2 s 146 / 156 / 134.
- Utilisation check: same 4 bands below the floor as the 4.6.6 law.
- Damage suite: sideslip within 1 deg except the one-fin held push-over, next item.

### One-fin held push-over: a sensitive case, in every build
The suite's two entry speeds read 7.1 / 5.7 / **13.4** deg (560 km/h) and 8.6 / **12.5** / 9.3 deg (600 km/h). The peak comes after the sticks are released at about -8 deg AoA, with the remaining rudder pinned at its stop for 0.6 s. Sweeping the entry speed from 525 to 635 km/h in 5 km/h steps (23 runs per build):

| | 4.6.6 law | 4.6.7 | 4.7.0 |
|---|---|---|---|
| Runs with more than 8 deg after release | 2 | 3 | 3 |
| Mean peak after release (deg) | 4.1 | 4.7 | 4.2 |
| Worst (deg) | 13.1 | 15.0 | 13.4 |

Every build has it at a few speeds; which ones is down to the state at release. Not caused by this change, and not fixed by it.

## Checks
- `RateCapFromHold=false` against 4.6.7 with `RollRateCapSpan=false` on three manoeuvres: 332 common columns identical.
- Estimator on the worker thread's copies (the game's path) against the main-thread path: 332 columns identical.
- The DLL against 4.6.7's: same 115 types, the embedded `F22E_FcsCore.dll`, methods +`ReversalRead`, `get_RateCapFromHold`, -`get_RollRateCapSpan`.
- Cost: five more allocator solves a tick. Whole tick on the bench machine with everything on the main thread 1.21 -> 1.34-1.48 ms; in the game the estimator runs on the worker thread.

## Open items
1. **The F-22E's surface rate saturation is still there** (game 12-29 % of samples at the servo rate in the windows above). Next step: test the stall-stagger explanation on the 80.8-84.6 s replay, which the bench reproduces.
2. One-fin held push-over, post-release yaw excursion (above). In every build.
3. Not flown.

## A/B in flight
`6. Lateral governor / RateCapFromHold`: true = 4.7.0, false = the 4.6.6 law, takes effect at once.

## Files
`CapacityEstimator.cs` (the reversal read), `EstimatorJob.cs`, `FcsController.cs` (the kick, the `aOpp` read, the log columns), `Plugin.cs` (the key), `FcsStandalonePlugin.cs` (version). Analysis scripts in `analysis/rate_saturation/`.
