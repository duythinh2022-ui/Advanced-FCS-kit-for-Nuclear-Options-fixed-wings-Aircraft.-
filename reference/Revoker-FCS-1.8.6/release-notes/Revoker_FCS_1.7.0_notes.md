# FS-12 Revoker FCS 1.7.0: the roll-rate cap reads the yaw capacity from the hold allocation

Base: 1.6.9. Same change in the F-22E FCS 4.7.0. The Su-30 FCS (0.6.6) still has the 1.6.9 half-span read.

**Status:** compiled, bench replays of the 24 Nov 02:21 flight log, held rolls and the full suite A/B run (below). The new DLL has **not been loaded in the game and not flown**.

Tom (24 Nov): "claude/FCS_surface_rate_saturation_findings_2026-11-24.md builfd for revoker and raptor first, find a better, cleaner way if u can".

## What was wrong
In held loaded rolls the canards and ailerons ran at their servo rate limit (100 deg/s) 30-55 % of the time (02:21 log, 46-57 s). The findings doc put this on the allocator asking for moves the servos cannot make in a tick and proposed a reach model on every effector. That is not the driver:

- **The bench reproduces the log once the replay matches the flight** (TVC off, the AoA cap that was flown, the 1.6.8 law). Game against bench in the 46-57 s window: mean surface travel 62 against 36 deg/s, canards at the rate limit 43-56 % against 51-65 %. The "bench is 7x calmer" figure in the 1.6.9 notes came from replaying with TVC on.
- **Reach on every effector was built and tested** (hard reach window, and the allocator's reach model on all effectors). Mixed to worse on the same replays and a lower roll rate in places. Rejected.
- **The driver is a loop through the governor.** The roll-rate cap (the reversal model behind `RollPaceRateBlend`) reads a yaw capacity from the capacity estimator. The estimator probed the allocator starting from this tick's surface command. The command follows the roll target (through the target's feed-forward), the roll target follows the rate cap, and the rate cap followed that reading: capacity -> rate cap -> roll target -> feed-forward -> command -> capacity. In the log that loop runs as a limit cycle (a self-sustained swing): cap 15-40 deg/s at about 1.8 Hz, roll demand +-60 deg/s^2.

The reading had two faults, and both reach the surfaces (46-57 s replay, rate cap in deg/s):

| Law | Slow swing of the cap (0.5-5 Hz, rms) | Tick-to-tick jump of the cap (mean) | Surface travel (deg/s) |
|---|---|---|---|
| 1.6.8 (as flown) | 1.94 | 1.51 | 35.9 |
| 1.6.9 (half-span) | 0.85 | 0.90 | 34.1 |
| 1.7.0 | 0.47 | 0.08 | 16.5 |

- The slow swing is the loop.
- The tick-to-tick jump comes from the probe being one allocator iteration away from wherever the command sat that tick. A 1.5 deg/s jump of the cap per tick is about 90 deg/s^2 of target rate, and the target's feed-forward passes it to the roll demand.

1.6.9's half-span read summed both sides of the same command-based reading. It cut the slow swing by half, left most of the jump, moved the surfaces 5 % less and cost mid-AoA roll rate (350 km/h, 22 deg: mean 79 -> 66 deg/s).

## Change
- The capacity estimator gets one more read, **the reversal read** (`capRevP` / `capRevN`), and the roll-rate cap is its only consumer.
- It starts from the **hold allocation** instead of this tick's command. The hold allocation is the set of surface positions that would make zero angular acceleration at the present rates: the allocator's answer to `hold = mNowF - I*wdotF`, which is INDI's own estimate of everything the effectors are *not* doing, with the sign turned. It moves with the flight condition, not with the rate loops, so the loop above is open.
- The hold allocation is the estimator's own state from tick to tick (one solve a tick, warm-started from its last answer), and each of its six probes continues from its own last answer. The figure is converged and continuous instead of one iteration away from wherever the command was.
- A side's figure is what the allocator reaches from the hold allocation, and never less than what the yaw axis is already doing toward that side (measured + committed).
- Same composite as before (the coordinated-rotation read and the yaw-only read, weighed by `rudW`), every part taken from the hold allocation.
- **Everything else reads this tick's command exactly as in 1.6.8**: the pitch/roll trade, the push governor and the pace. They divide the authority of this tick, and the command is what they are dividing. The rate cap plans a reversal that starts later, and it is the one consumer that sets the roll target.
- The 1.6.9 half-span read and its key `RollRateCapSpan` are gone.
- New dev key **`6. Lateral governor / RateCapFromHold`** (true). false = the 1.6.8 law exactly, switchable in flight for an A/B.
- Four new log columns after `ceGUpdX`: `ceRevP`, `ceRevN` (the figure the cap reads), `ceHoldZ`, `ceHoldX` (yaw / roll acceleration left over at the hold allocation; 0 = the hold is reachable).
- The native core (`Revoker_FcsCore.dll`) is unchanged.

### How the split was chosen
The first build moved every consumer to the hold read. On the Revoker that worked; on the F-22E it regressed the suite (pull-then-roll 250: AoA overshoot 45.6 -> 54.1, sideslip 1.2 -> 3.2). A bench build with both reads and a per-consumer switch, all 16 combinations on both jets:

- Rate cap alone on the hold read gives the whole benefit on the Revoker (replay travel 16.5 / 13.8 / 14.6 deg/s against 16.5 / 15.1 / 14.6 with every consumer on it, 35.9 / 31.5 / 26.3 with none).
- The trade on the hold read is what regressed the F-22E; the push governor added sideslip in its push cases; the pace changed nothing on either jet.
- Taking `rudW` from the command-based probes to save two solves brought part of the swing back (replay travel 21.0 / 15.8 / 20.9, held rolls up to 36.8). Rejected.

## Bench: replay of the 02:21 log (TVC off, as flown)
Mean surface travel = mean of |surface rate| over all surfaces, deg/s. "At limit" = share of samples at 90 % or more of the 100 deg/s servo rate.

| Window (s) | Law | Travel | At limit | Roll rate | Roll-demand swing (nuR band, deg/s^2) | Rate-cap swing (band, deg/s) |
|---|---|---|---|---|---|---|
| 46.2-57 | 1.6.8 (as flown) | 35.9 | 10.3 % | 24.2 | 21.9 | 3.58 |
| | 1.6.9 | 34.1 | 10.5 % | 23.0 | 13.6 | 1.34 |
| | **1.7.0** | **16.5** | 4.3 % | 25.2 | 8.3 | 1.13 |
| 96.7-103.2 | 1.6.8 | 31.5 | 6.5 % | 40.3 | 27.3 | 2.27 |
| | 1.6.9 | 27.5 | 3.6 % | 38.5 | 16.9 | 1.67 |
| | **1.7.0** | **13.8** | 1.3 % | 42.5 | 17.0 | 2.00 |
| 120.3-126.8 | 1.6.8 | 26.3 | 2.2 % | 40.8 | 20.4 | 5.48 |
| | 1.6.9 | 25.5 | 2.5 % | 38.9 | 9.1 | 0.87 |
| | **1.7.0** | **14.6** | 1.2 % | 42.1 | 5.1 | 2.59 |

The game's own figure in the first window is 62 deg/s, so the bench reads about 0.6x the game here. Expect the direction, not the exact size.

## Bench: held loaded rolls
Stick held aft, full roll held from 3 s, 12 s runs. Travel in deg/s, 1.6.8 law / 1.6.9 / 1.7.0.

| Case | Travel | At limit, 1.6.8 / 1.6.9 / 1.7.0 | Roll rate, 1.6.8 -> 1.7.0 |
|---|---|---|---|
| 300 km/h, 0.45 aft, TVC on | 24.6 / 22.0 / **13.4** | 9.0 / 3.7 / 2.8 % | |
| 460 km/h, 0.45 aft, TVC on | 24.4 / 13.9 / **10.7** | 6.1 / 0.9 / 0.6 % | 48.4 -> 49.1 (1.6.9: 42.5) |
| 400 km/h, full aft, TVC on | 23.5 / 22.2 / 24.1 | 9.2 / 8.7 / 11.0 % | unchanged |
| 300 km/h, 0.45 aft, TVC off | 49.3 / 33.2 / **23.4** | 25.7 / 14.8 / 4.9 % | |
| 460 km/h, 0.45 aft, TVC off | 32.5 / 26.7 / **18.9** | 7.0 / 6.7 / 4.4 % | |
| 400 km/h, full aft, TVC off | 55.2 / 34.8 / **31.2** | 33.3 / 15.2 / 14.4 % | |

## Bench: full suite, 1.6.8 law / 1.6.9 / 1.7.0
Roll rates are back to the 1.6.8 law's or above it. Sideslip is 0.1-0.8 deg higher in places.

- Roll-stop / reversal sweep (16 conditions, stop and reversal each): mean roll 88 / 88 / 90 deg/s. Stop sideslip worst 4.0 / 4.0 / 4.6, mean 2.07 / 2.06 / 2.24. Reversal sideslip worst 6.2 / 6.4 / 6.6, mean 3.02 / 2.98 / 3.14.
- **Largest shift, 650 km/h 0.55 aft:** mean roll 135 / 136 / 154 deg/s (peak 194 / 193 / 227), stop sideslip 4.0 / 4.0 / 4.6, reversal 6.2 / 6.4 / 6.6. The cap lets go about 0.2 s earlier in the roll (see open items).
- 500 km/h 0.40 aft: mean roll 103 / 102 / 108, peak 137 / 134 / 153, stop sideslip 3.4 / 3.4 / 3.8.
- held0.4+roll 350 (22 deg AoA): peak / mean roll 124 / 79, 91 / 66, **125 / 84**. 1.6.9's cost is gone.
- pull-then-pushover+roll 350: peak roll 87 / 88 / 113, sideslip 2.5 / 2.5 / 3.0, surface travel 26.6 -> 32.1 deg/s (the one case with more travel).
- push+roll 300: peak roll 79 / 80 / 89, sideslip 1.2 / 1.2 / 1.4.
- roll-then-pull-rev 400: sideslip 3.8 / 4.6 / 4.6.
- fullaft+roll 250: sideslip 2.9 / 2.3 / 2.0.
- Rolls and releases at 64-66 deg AoA (pull-then-roll, the 65 AoA releases): unchanged within 1 deg/s and 0.3 deg. fullaft+roll+rev3s 500 (57 deg): peak roll 108 / 108 / 113.
- Damage suite: within 0.8 deg of sideslip; clean falling leaf at 296 km/h peak velocity-roll 61 / 57 / 78 deg/s (370 km/h: 65 / 74 / 73).

### Response curves (every case, stick-in window)
| Block | Surface travel (deg/s) | Surface reversals (/s) | Roll-target retreats with the stick held (deg/s) | Roll-rate dips | Roll onset |
|---|---|---|---|---|---|
| Corkscrews (20) | 23.6 / 20.0 / **16.8** | 7.2 / 5.8 / **3.7** | 12.6 / 5.1 / **4.1** | 1.5 / 0.7 / 0.9 | 0.21 s, unchanged |
| Regression set (20) | 21.5 / 20.9 / 20.4 | 5.4 / 5.2 / 4.7 | 22.6 / 19.8 / 20.3 | 10.5 / 10.1 / 9.9 | 0.23 s, unchanged |
| Sweep (32) | 20.7 / 20.4 / 20.9 | 4.7 / 4.4 / 4.4 | 16.8 / 13.5 / 16.1 | 33.9 / 34.1 / 34.7 | 0.22 s, unchanged |

Against the 1.6.8 law, case by case:
- Corkscrews: fewer surface reversals in all 20 cases; no case worse on any measure.
- Regression set: pull-then-pushover+roll 350 moves its surfaces more (travel 26.6 -> 32.1 deg/s, reversals 6.9 -> 8.7 /s) with its higher roll rate. The roll target's retreat with the stick held is larger in 5 cases (fullaft+roll+rev3s 320: 4 -> 16 deg/s; 65 AoA roll release: 10 -> 15; roll-then-pull-rev 400: 26 -> 32; pull-then-pushover+roll 450: 22 -> 26; roll-then-push -55: 42 -> 50) and smaller in 4.
- Sweep: the retreat is larger in 5 cases (500 km/h 0.40 reversal 26 -> 76 deg/s, 650 km/h 0.55 stop 9 -> 33, 650 km/h 0.55 reversal 42 -> 108, where 1.6.9 has 111) and smaller in 6. This is the cap letting go and binding again, see open items. Reversals from the higher roll rates pass through a larger rate change (500 km/h 0.40: 88 -> 100 deg/s).
- Pitch-command hitches: unchanged in all three blocks.

## Checks
- `RateCapFromHold=false` against 1.6.9 with `RollRateCapSpan=false` on the 46-57 s replay: 392 common log columns identical.
- Estimator on the worker thread's copies (the game's path) against the main-thread path: all flight columns identical.
- The DLL against 1.6.9's: same 121 types, the embedded `Revoker_FcsCore.dll`, methods +`ReversalRead`, `GainSumZ`, `get_RateCapFromHold`, -`get_RollRateCapSpan`.
- Cost: seven more allocator solves a tick. Estimator time on the bench machine 370 -> 620 us per tick (measured on the main thread; in the game it runs on the worker thread).

## Open items
1. **Full aft with TVC on (70 deg AoA) is unchanged**: 23.5 -> 24.1 deg/s, 9 -> 11 % at the limit. Not the governor loop.
2. **TVC off, full aft at the 45 deg cap**: 55 -> 31 deg/s, still 14 % at the limit. The canards hunt at the cap with the roll target out of reach.
3. **The rate cap lets go in one step.** When the reversal capacity reaches the demand (`aOpp >= aD`) the cap goes from about 110-140 deg/s to unbound in one tick and the roll target jumps (650 km/h 0.55: 154 -> 231 deg/s). 1.6.8 does the same about 0.2 s later in the roll (174 -> 242); the hold read is higher there, so 1.7.0 gets there sooner. That is where the higher roll rate and the extra 0.6 deg at 650 km/h come from. Making the cap open continuously is a change to the reversal model, not done here.
4. Not flown. The bench reads about 0.6x the game's surface travel on this log.

## A/B in flight
`6. Lateral governor / RateCapFromHold`: true = 1.7.0, false = the 1.6.8 law, takes effect at once. In the log, `rateCap` and `ceRevP` / `ceRevN` should hold steady in a held loaded roll where `rateCap` swung 15-40 deg/s before.

## Files
`CapacityEstimator.cs` (the reversal read), `EstimatorJob.cs` (passes the hold moment through), `FcsController.cs` (the kick and the `aOpp` read), `Plugin.cs` (the key), `FcsStandalonePlugin.cs` (version). Analysis scripts in `analysis/rate_saturation/`.
