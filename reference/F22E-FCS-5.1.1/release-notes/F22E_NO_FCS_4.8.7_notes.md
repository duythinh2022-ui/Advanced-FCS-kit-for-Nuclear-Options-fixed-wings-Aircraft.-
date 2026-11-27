# F-22E Strike Raptor FCS 4.8.7 (26 Nov 2026)

Ask: Tom 26 Nov 01:04 "find a way to fix it" (the 4.8.6 reversal sideslip with nozzles on) and 01:14 "why would those problems be there in the first place". Bench only; not flown.

## Change (one law, no new gate or setting)
The yaw-rate loop's feed-forward (`FcsController.LateralLaw`, `rDotFF`). The velocity-vector roll is planned as one stability-axis rate pS (trajectory `pTraj`); the body commands are pCmd = pS cos AoA, rCmd = pS sin AoA (+ the sideslip term).
- Before: roll feed-forward = the trajectory's planned acceleration x cos AoA (exact, on time). Yaw feed-forward = the rate of rCmd differentiated and passed through a 0.1 s filter. So every time the roll trajectory accelerated or braked, the yaw axis was asked for its share later than the roll axis.
- Now: yaw feed-forward = (planned pS' x sin AoA + pS x cos AoA x measured AoA rate) / scale, the exact derivative of the coordinated command. Only the sideslip term still goes through the 0.1 s derivative filter.
- A first version took only the pS' part exactly and left the AoA-rate part filtered: rolling pulls and unloaded rolls got worse (the two halves of the same derivative arrived 0.1 s apart). Both parts exact fixed that.
- Bench trace that showed it (45 deg limit, TVC, reversal): 0.2 s after the stick the roll target was -100 deg/s^2 and the yaw target -46, for the same commanded rate change.

`StabSplitCost` (4.8.6) stays. With the yaw asked on time, the stab split cost no longer shows up in the reversals.

## Bench, two-zone (Tom's airframe), 450 km/h, limits 30/36/45/50
| | 4.8.2 | 4.8.6 | 4.8.7 |
|---|---|---|---|
| Reversal sideslip, TVC | 2.7/3.5/4.2/2.9 | 5.7/3.8/6.2/5.0 | 2.6/2.2/3.2/2.2 |
| Reversal sideslip, no TVC | 2.7/3.4/4.2/3.6 | 3.7/2.7/4.6/3.5 | 1.2/1.5/2.8/2.4 |
| Reversal roll, TVC, deg/s | 78.2/69.3/57.0/51.5 | 77.8/72.4/56.4/51.4 | 79.6/72.8/56.1/51.3 |
| Reversal roll, no TVC | 64.9/55.3/54.7/51.5 | 68.6/59.8/54.6/52.0 | 64.3/58.0/55.5/51.7 |
| Loaded roll from 36/45, TVC | 150.5/117.3 | 143.1/114.0 | 152.7/122.9 |
| Loaded roll from 36/45, no TVC | 148.2/94.3 | 141.2/112.8 | 140.4/114.2 |
| Rolling pull sideslip 36/45, TVC | 3.7/4.8 | 6.3/5.8 | 5.3/5.6 |
| Rolling pull sideslip 36/45, no TVC | 10.0/9.0 | 5.7/4.8 | 7.5/9.4 |
| Release grid (18 cases) worst sideslip mean / max | | 5.8/8.6 (4.8.4) | 4.8/6.9 |

Low-AoA battery (rolls at 300/450 km/h, pushes): within 1 deg/s or better; worst sideslip equal or lower in 10 of 12 roll cases. Idle stab split around a roll: 0.0-0.1 mean.

## X2e (TwoZoneAirframe off)
| | 4.8.6 | 4.8.7 |
|---|---|---|
| Reversal sideslip, TVC | 3.5/4.8/5.8/5.0 | 4.9/3.9/4.4/4.1 |
| Reversal sideslip, no TVC | 3.8/1.3/2.3/3.5 | 4.1/1.0/1.2/3.6 |
| Rolling pull sideslip 36/45, TVC | 4.6/2.7 | 8.9/6.8 |
| Rolling pull sideslip 36/45, no TVC | 4.4/3.6 | 4.6/3.4 |

## Worse
- Rolling pull, roll stick released while pulling (pull case: full roll 0-1.5 s, full aft from 0.3 s): two-zone no TVC 5.7/4.8 -> 7.5/9.4 (4.8.2 was 10.0/9.0), X2e TVC 4.6/2.7 -> 8.9/6.8.
- Mechanism (two-zone no TVC, 45 deg): the roll stops at 1.5 s with the AoA rising through 28-33; the yaw is now asked to brake on time, but the rudders are at +35 and need about 0.7 s to swing across. The yaw is unmet (-25...-34 deg/s^2), the allocator (yaw weighted about 13x roll) takes it from surfaces that also roll, and the roll overshoots its stop to -13 deg/s while the yaw is still 23-38 deg/s: sideslip -9.4.
- The roll trajectory's pace comes from the yaw authority over the rudders' whole travel (`aYaw`), not from where the rudders are now. A cap of the trajectory at aYaw / sin AoA was tried and never bound. The roll stop needs to be planned with the rudders' travel time, as the loaded reversal already is (`RudderCrossTime`). Not done here.

## Why these problems existed (01:14 question)
- The governors pace the stability-axis roll command from the models. The models were not wrong in this case. The yaw axis was handed its share of that command 0.1 s late by the law itself (a filtered derivative where the roll got the planned value). That was in 4.8.2 too: 4.8.2's own reversal sideslip (2.7-4.2) was partly this.
- In 4.8.3-4.8.5 a drifting stab split happened to give the yaw a head start in reversals and hid it; 4.8.6 stopped the drift and it showed.
- The remaining stop case is a pacing gap of the same kind: the pace uses yaw authority as if the rudders could deliver it at once.
