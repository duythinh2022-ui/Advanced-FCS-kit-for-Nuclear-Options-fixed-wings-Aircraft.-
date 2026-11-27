# FS-20 Vortex FCS 0.8.1: pull trade against the roll the jet has

Base: 0.8.0. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 13:48): "fix" (the open list of the last builds), 13:49: "no ontop fixes".

## Change (shared code; found on the J-20B, see J20B_FCS_0.2.1_notes)
- **Pull trade against the roll the jet has (`TradeRollHave`, 6. Lateral governor).** Pulling or pushing under a roll, the trade lowers the roll target and gives the rest of the yaw shortfall to the AoA rate. That AoA rate was reckoned against the roll target the trade had just set, as if the roll were already there. The roll takes 0.4-0.6 s to come down to it, and meanwhile the AoA swept at the rate that suits the slower roll. It is now reckoned against the roll the jet has (measured, never more than the stick asks, never less than the target). false = the old law.

## Bench (regression set, 20 cases), 0.8.0 -> 0.8.1
- Mean sideslip 3.60 -> 3.52 deg, mean roll 41.5 -> 41.6 deg/s.
- Push + roll corkscrew 170 km/h: 3.1 -> 2.3 deg (0.7.3: 1.7). No other case moves by more than 0.7 deg.
- With `LateralReserveBacked` + `PlanHoldNozzleShare` on: mean 4.12 -> 4.05. Still worse than off; they stay off.

## Vortex Family FCS 0.8.1
Built from the same tree (same family fingerprint).

## Also in the code, off: `ReachHeldStick` (4. Gains, dev)
The allocator's reach model on the stabilators with the pitch stick held (see Revoker_FCS_1.8.1_notes: it calms the Revoker's canards with TVC off, at a cost in pushovers under roll). Shared code, default false, **not benched on this jet**.
