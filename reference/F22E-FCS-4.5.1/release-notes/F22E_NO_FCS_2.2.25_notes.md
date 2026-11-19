# F-22E FCS 2.2.25: the pitch-rate dip when you pull into an established roll

Base: 2.2.24. Log: 20 Oct 01:41 (nozzles on). Bench: your live Dev cfg with `DisableTVC = false`.

## What the log shows
Rolling at 175-210 deg/s, then full aft with the roll held:

| t (s) | IAS | Pitch-rate command after the pull | Roll | Sideslip |
|---|---|---|---|---|
| 233.6 | 264 kt | 37 -> 31 -> 34 -> 46 -> 42 | 177 -> 145 -> 126 | 6.0 |
| 307.2 | 229 kt | 31 -> 22 -> 24 -> 29 -> 41 -> 48 | 179 -> 82 -> 96 | 8.7 |
| 348.6 | 252 kt | 34 -> 30 -> 32 -> 41 -> 48 | 211 -> 80 -> 89 | 8.0 |

Each time, the same sequence:
1. **The AoA starts rising with the roll still at full rate.** The roll-trade feed-forward (2.2.11) only stops a roll from building; it didn't cut one already running.
2. **At 175-210 deg/s, each degree per second of AoA rise drives the sideslip hard** (it goes as roll rate x AoA rate). The 2.2.24 boost had raised the allowed rise in the first 0.3 s while the sideslip was still near zero.
3. **The governor caught the sideslip at 5-9 deg** and cut the AoA rise. Only then did the trade cut the roll, 211 -> 80 deg/s in 0.5 s.
4. **The sideslip came back down and the governor let the AoA go again.** That gives the 32-24-30-28-34-48 you felt.

**At 146 kt (32 s)** the roll was ~100 deg/s and this didn't happen. That case is the 00:58 log's, and 2.2.24's change was for it.

## Changes
1. **The roll trade cuts an established roll at low AoA** (`PullTradeCutA` 15, internal).
   - Below 15 deg AoA (fading out to 25), the trade slows a roll already running to what the asked AoA rise can coordinate, as soon as you pull, not after the sideslip has built.
   - At low AoA a velocity-vector roll is nearly a body roll, so slowing it doesn't swing the sideslip. That swing was why 2.2.11 left established rolls alone; it was seen at 35 deg AoA.
2. **The 2.2.24 boost backs off as soon as the sideslip is building fast** (`PullFFBoostSlipFall`, internal), not only near the margin.
   - It falls in proportion as the kinematic sideslip rate on the AoA-rise side passes 2 deg/s, fully at 4.
   - The AoA rise eases instead of being stopped late by the taper.

**Tried and not kept:**
- **Each change on its own:** about half the improvement.
- **Holding the roll trade on the measured AoA rise:** no effect.
- **Netting the yaw the jet adds out of the governor's sideslip projection:** the sideslip in these pulls is real (9 deg/s), so the projection wasn't the fault. It made the dips worse.

## Bench
**Replays of your four pulls** (264 / 229 / 252 / 242 kt, each at +-10 km/h; 12 cases):

| | 2.2.24 | 2.2.25 |
|---|---|---|
| Pitch-rate dip, mean / worst | 2.6 / 6.4 deg/s | 0.2 / 2.2 deg/s |
| Pitch-rate command dip, mean / worst | 4.8 / 9.8 | 0.9 / 3.1 |
| Sideslip worst, mean / max | 5.2 / 5.7 deg | 3.5 / 4.6 deg |
| Time to 30 deg AoA, mean | 2.6 s | 2.1 s |
| Roll 1 s after the pull, mean | 117 deg/s | 80 deg/s |

**Full roll established, then full aft** (189 / 227 / 259 / 297 kt):

| | 2.2.24 | 2.2.25 |
|---|---|---|
| Pitch-rate dip | 33.6 / 23.3 / 10.4 / 12.5 deg/s | 0.6 / 1.2 / 1.4 / 5.0 |
| Sideslip worst | 9.4 / 8.0 / 5.3 / 4.6 | 2.2 / 3.2 / 4.3 / 5.2 |
| Time to 30 deg AoA | 3.1 / 2.9 / 3.0 / 3.1 s | 1.9 / 1.9 / 2.0 / 2.4 s |

**Same with a 0.7 pull:** dips 2-15 -> 1-16 deg/s. The 297 kt 0.7-pull case dips ~15 in every version. At 189-259 kt the AoA now reaches 30 deg in 2.8-3.9 s; before, it didn't within 5 s.

**The cost:** the roll is slowed sooner, to ~80 deg/s 1 s into the pull instead of ~117 (the PullRollTrade 0.7 setting doing its job earlier).

**00:58 log replays (low IAS, 2.2.24's case; 40 jittered runs):**
- Time to 30 deg AoA, mean: 1.87 s (2.2.23), 1.74 s (2.2.24), 1.78 s (2.2.25). At the 28 s pull (92-111 kt): 2.2 / 1.7 / 1.9 s.
- Sideslip while the AoA rises stays under 7 deg.
- At the 65 deg limit with the roll held, the 0.9-throttle cases reach 11.1-11.5 deg (2.2.24: 10.5-10.6, 2.2.23: 8.7-10.1). The governor doesn't act there; it's the yaw at the AoA limit.

**Regression:**
- Suite (nozzles on and off) and hitch set: identical.
- Fins gone, rolling pulls: worst sideslip 17.7 -> 15.6 deg; the rest unchanged.

## Not verified
- Not flown. Bench only.
