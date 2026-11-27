# F-22E Strike Raptor FCS 4.8.8 (26 Nov 2026)

Ask: Tom 26 Nov 01:18 "do it": plan the roll stop with the rudders' current position and travel time (the 4.8.7 rolling-pull sideslip). Bench only; not flown.

## Change (one rule in the roll plan, no setting)
`FcsController.LateralLaw`, the roll trajectory `pTraj`. A velocity-vector roll changes its yaw share at pS'' x sin AoA (jerk). The yaw acceleration can only build as fast as the rudders get from where they are to the stop that yaws the jet the way the plan needs (`RudderTimeToward`, new: per rudder, |stop - now| / servo rate, stop chosen from the rudder's own moment model). The plan's jerk, both build-up and run-out, is held to aYaw / (that time x |sin AoA|).
- Rudders already on the needed side: short time, no limit. Low AoA (small sin AoA): no limit.
- The loaded reversal already paced its build-up by `RudderCrossTime` (time to the far stop whatever the direction); that stays.
- A cap on the plan's acceleration at aYaw / sin AoA was tried first and never bound: the problem is how fast the yaw can build, not how much there is.

## Bench (450 km/h, limits 30/36/45/50)
Rolling pull, roll released while pulling, worst sideslip 36 / 45:
| | 4.8.2 | 4.8.6 | 4.8.7 | 4.8.8 |
|---|---|---|---|---|
| Two-zone TVC | 3.7 / 4.8 | 6.3 / 5.8 | 5.3 / 5.6 | 5.0 / 5.6 |
| Two-zone no TVC | 10.0 / 9.0 | 5.7 / 4.8 | 7.5 / 9.4 | 5.6 / 3.8 |
| X2e TVC | 6.5 / 4.9 | 4.6 / 2.7 | 8.9 / 6.8 | 3.2 / 2.8 |
| X2e no TVC | 4.0 / 4.0 | 4.4 / 3.6 | 4.6 / 3.4 | 4.5 / 3.5 |

Reversal sideslip:
| | 4.8.2 | 4.8.7 | 4.8.8 |
|---|---|---|---|
| Two-zone TVC | 2.7/3.5/4.2/2.9 | 2.6/2.2/3.2/2.2 | 2.4/2.0/2.7/2.1 |
| Two-zone no TVC | 2.7/3.4/4.2/3.6 | 1.2/1.5/2.8/2.4 | 3.0/1.5/2.6/2.3 |
| X2e TVC | 3.4/4.9/5.8/4.7 | 4.9/3.9/4.4/4.1 | 4.0/3.2/5.1/4.0 |
| X2e no TVC | 2.9/1.3/2.2/3.6 | 4.1/1.0/1.2/3.6 | 3.4/1.0/1.1/3.4 |

Reversal roll rates within 1.2 deg/s of 4.8.7 everywhere. Loaded roll from 36/45: two-zone TVC 151.2/122.2, no TVC 143.0/113.3. Release grid (18 cases, two-zone): worst sideslip mean 4.7, max 6.6 (4.8.7: 4.8 / 6.9). Low-AoA rolls and pushes unchanged; worst sideslip equal or lower in 11 of 12 roll cases.

## Cost
The roll stops later, because it now stops at the pace the yaw can follow. Roll carried after the stick is released from a full reversal (deg), 30/36/45/50:
| | 4.8.2 | 4.8.7 | 4.8.8 |
|---|---|---|---|
| Two-zone TVC | 30.1/25.6/25.1/26.4 | 30.0/24.6/25.0/24.8 | 32.0/29.1/25.3/26.2 |
| Two-zone no TVC | 22.2/17.0/20.5/25.5 | 21.1/19.2/22.6/23.8 | 26.3/22.1/25.9/27.3 |
| X2e TVC | 24.3/17.6/15.2/17.2 | 25.0/16.5/14.6/18.0 | 29.6/20.8/16.6/21.3 |
| X2e no TVC | 13.3/9.7/8.4/8.0 | 13.7/9.3/8.4/8.5 | 15.5/10.1/11.3/9.0 |

Stop time (to under 2 deg/s) +0.00-0.10 s. Worse than 4.8.7: two-zone no-TVC reversal sideslip at the 30 deg limit (1.2 -> 3.0; 4.8.2 2.7).
