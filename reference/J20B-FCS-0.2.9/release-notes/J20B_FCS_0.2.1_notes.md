# J-20B Weilong FCS 0.2.1: the pull-through under full roll

Base: 0.2.0. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 13:48): "fix" (the open list of 0.2.0), 13:49: "no ontop fixes".

## What was wrong (bench, push-then-pull-through under full roll, the 450 km/h entry, 550-610 km/h at the pull)
- 0.1.8 already made 27.5 deg of sideslip there, and the AoA went to -12 deg with 60 deg/s of pitch rate: the roll was turning AoA into sideslip.
- At the stick reversal the trade cut the roll target to 81-126 deg/s. The roll was still 170-185 deg/s for half a second. The pitch rate went -18 -> +27 deg/s in 0.4 s, the fins had to cross stop to stop, and the yaw rate stayed 30-45 deg/s behind its command for the rest of the manoeuvre.

## Change
- **Pull trade against the roll the jet has (`TradeRollHave`, 6. Lateral governor).** Pulling or pushing under a roll, the trade lowers the roll target and gives the rest of the yaw shortfall to the AoA rate. That AoA rate was reckoned against the roll target the trade had just set, as if the roll were already there. The roll takes 0.4-0.6 s to come down to it, and meanwhile the AoA swept at the rate that suits the slower roll. It is now reckoned against the roll the jet has (measured, never more than the stick asks, never less than the target). false = the old law.

With `TradeRollHave` false the bench log is identical to 0.2.0.

## Bench: push-then-pull-through under full roll (1500 m)
| Entry | 0.1.8 | 0.2.0 | 0.2.1 | Mean roll (deg/s) | Highest AoA reached |
|---|---|---|---|---|---|
| 350 km/h | 7.0 | 12.2 | **8.9** deg of sideslip | 86 -> 87 | 23.2 -> 27.8 |
| 450 km/h | 27.5 | 23.2 | **14.2** | 134 -> 133 | 13.2 -> 20.0 |

- The roll is not slower. The pull is, for the half second the roll needs to come down.
- 350 km/h is still 1.9 deg worse than 0.1.8. That part is the roll cap fix (with `RateCapFromHold` false it is 7.0).
- 14 deg at the 450 km/h entry is still a lot. What is left: the pitch-roll inertial yaw, (Iy - Ix) x roll rate x pitch rate, about 150 deg/s^2 at 180 deg/s of roll and 60 deg/s of pitch, which nothing in the governor counts. A roll-rate cap built on it was tried (the yaw the allocator can still add from the hold allocation against the inertial yaw of the commanded roll): it never bound in this manoeuvre and changed nothing, so it is not in the build.

## Bench: the 0.2.0 check set at the 45 deg limit
All 20 cases identical to 0.2.0 (pulls, full-aft rolls, reversals, mid-AoA rolls, the loop).

## Tried, not shipped
- `LateralReserveBacked` + `PlanHoldNozzleShare` on with this fix: still 15.2 / 24.3 deg in the two cases. They stay off.

## Also in the code, off: `ReachHeldStick` (4. Gains, dev)
The allocator's reach model on the stabilators with the pitch stick held (see Revoker_FCS_1.8.1_notes: it calms the Revoker's canards with TVC off, at a cost in pushovers under roll). Shared code, default false, **not benched on this jet**.
