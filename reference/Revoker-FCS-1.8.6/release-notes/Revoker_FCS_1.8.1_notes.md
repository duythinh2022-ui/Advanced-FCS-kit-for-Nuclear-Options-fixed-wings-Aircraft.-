# FS-12 Revoker FCS 1.8.1: pull trade against the roll the jet has

Base: 1.8.0. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 13:48): "fix" (the open list of 1.8.0), 13:49: "no ontop fixes".

## Change (shared code; found on the J-20B, see J20B_FCS_0.2.1_notes)
- **Pull trade against the roll the jet has (`TradeRollHave`, 6. Lateral governor).** Pulling or pushing under a roll, the trade lowers the roll target and gives the rest of the yaw shortfall to the AoA rate. That AoA rate was reckoned against the roll target the trade had just set, as if the roll were already there. The roll takes 0.4-0.6 s to come down to it, and meanwhile the AoA swept at the rate that suits the slower roll. It is now reckoned against the roll the jet has (measured, never more than the stick asks, never less than the target). false = the old law.

## Bench, 1.8.0 -> 1.8.1
- Regression set, TVC on (20 cases): mean sideslip 2.28 -> 2.29 deg, mean roll 42.9 -> 42.9 deg/s. No case moves by more than 0.7 deg or 5 deg/s.
- Regression set, TVC off: mean sideslip 3.71 -> 3.70, mean roll 33.9 -> 33.9. Same.
- This jet rolls at 40-110 deg/s in these cases, the J-20B at 130-190: the half second the roll needs to come down matters less here.

## The canards crossing their stall in TVC-off rolls: a lever found, shipped OFF, not fixed
**What it is (12:06 log, 42.3-45 s, 405 km/h, 45 deg cap, full left stick, in the game and in the replay):** the allocator sends the left canard from +57 deg (unstalled) to -45 deg (broadside to the airflow, 90 deg local AoA) for the yaw and roll it is short of. That is 100 deg of travel, a second at the servo rate. Before the canard gets there the demand has changed and it is sent back. Then the pair hops between their two lift peaks (15 deg and 40-50 deg local AoA) every 0.5-1 s for the pitch.

**The lever (`ReachHeldStick`, 4. Gains, dev, default false):** the allocator already has a reach model for this (a far plan has to pay for its transit), but for the stabilators / canards it was only on with the pitch stick released, in the capped sweep and under a planned push. The key puts it on with the stick held too.

| Seven replay windows, TVC off | 1.8.0 / 1.8.1 | `ReachHeldStick` on |
|---|---|---|
| Canard travel | 43.0 deg/s | 34.2 |
| Worst window | 73.7 deg/s | 49.5 |
| Time at the servo rate | 26.0 % | 16.6 % |
| Canard more than 10 deg past its band | 53 % of the time | 45 % |
| Mean roll rate | 24.8 deg/s | 25.5 |
| Peak sideslip (mean of the windows / worst) | 4.6 / 6.0 deg | 4.8 / 6.3 |

In the worst window the first swing is still there (+57 -> -15 deg and back, 0.7 s); the hopping after it is gone.

**Why it is off:** the regression set with TVC off gets worse in the pushovers under full roll. Mean sideslip 3.70 -> 3.94 deg, pull-then-pushover + roll 350: 4.2 -> 8.1, 450: 6.5 -> 9.2, pull-then-roll 400: 2.8 -> 4.2 (better: full aft + roll + reversal 500 6.4 -> 5.2, pull-through + roll 350 5.5 -> 3.9). In the 350 case the roll rate cap lets go through the sweep (32 deg/s -> none) with 15-38 deg/s^2 of yaw unmet, and the roll runs at 60-80 deg/s through zero AoA; without the key the roll target sits at zero for 0.9 s there. That is a governor question that has to be answered before this can be on. With TVC on the set is unchanged (2.29 -> 2.24).

**Ruled out:**
- The roll acceleration the trajectory flies: held to 120 deg/s^2 it was worse (travel 42 -> 60 deg/s, sideslip up to 10.5 deg).
- The lateral reserve (1.8.0 changes it; no effect on this).
- The reach model on the flaperons and ailerons as well: travel 34 -> 39 deg/s, worse.
- Weighting a far plan's transit by how long it lasts (native core experiment, Linux build only): travel 34 -> 44.5 deg/s, roll 25.5 -> 22.8 deg/s. Worse, so the native core is unchanged.
