# T/A-30 Compass FCS 0.7.1: pull trade against the roll the jet has

Base: 0.7.0. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 13:48): "fix" (the open list of the last builds), 13:49: "no ontop fixes".

## Change (shared code; found on the J-20B, see J20B_FCS_0.2.1_notes)
- **Pull trade against the roll the jet has (`TradeRollHave`, 6. Lateral governor).** Pulling or pushing under a roll, the trade lowers the roll target and gives the rest of the yaw shortfall to the AoA rate. That AoA rate was reckoned against the roll target the trade had just set, as if the roll were already there. The roll takes 0.4-0.6 s to come down to it, and meanwhile the AoA swept at the rate that suits the slower roll. It is now reckoned against the roll the jet has (measured, never more than the stick asks, never less than the target). false = the old law.

## Bench (regression set, 20 cases), 0.7.0 -> 0.7.1
Mean sideslip 4.08 -> 4.10 deg, mean roll 81.3 -> 81.3 deg/s. No case moves by more than 0.7 deg or 5 deg/s.

## Correction to the 0.7.0 notes: the Compass does hold its AoA limit under roll
The 0.7.0 notes said the AoA "falls to 13-18 deg of 35 under full roll at full aft". That was a misreading of the regression set: in those cases the pull and the roll start together (or the roll first), so the figure is how far a pull has got after 2 s under full roll, where the trade slows it on purpose. Tested properly (full aft held until the limit is established, then full roll at 3 s, then a reversal; 1500 m):

| Case | 0.6.0 | 0.7.0 / 0.7.1 |
|---|---|---|
| Full roll at the limit, 250 km/h | 34.7-35.1 deg | 34.6-35.1 |
| same, 400 km/h | 34.6-35.1 | 34.6-35.2 |
| After the reversal, 250 km/h | 32.1-34.8 | **34.0-35.6** |
| After the reversal, 400 km/h | 33.7-35.0 | 34.1-35.5 |

(At 600 km/h the jet is on its G limit at 10 deg, not its AoA limit.) So there was nothing to fix here, and `LateralReserveBacked` / `PlanHoldNozzleShare` stay off: with them on these cases are the same to within 0.2 deg.

## Also in the code, off: `ReachHeldStick` (4. Gains, dev)
The allocator's reach model on the stabilators with the pitch stick held (see Revoker_FCS_1.8.1_notes: it calms the Revoker's canards with TVC off, at a cost in pushovers under roll). Shared code, default false, **not benched on this jet**.
