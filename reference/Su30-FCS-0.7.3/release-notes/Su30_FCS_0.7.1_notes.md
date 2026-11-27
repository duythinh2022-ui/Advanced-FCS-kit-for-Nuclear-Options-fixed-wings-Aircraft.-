# Su-30VXN FCS 0.7.1: pull trade against the roll the jet has

Base: 0.7.0. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 13:48): "fix" (the open list of the last builds), 13:49: "no ontop fixes".

## Change (shared code; found on the J-20B, see J20B_FCS_0.2.1_notes)
- **Pull trade against the roll the jet has (`TradeRollHave`, 6. Lateral governor).** Pulling or pushing under a roll, the trade lowers the roll target and gives the rest of the yaw shortfall to the AoA rate. That AoA rate was reckoned against the roll target the trade had just set, as if the roll were already there. The roll takes 0.4-0.6 s to come down to it, and meanwhile the AoA swept at the rate that suits the slower roll. It is now reckoned against the roll the jet has (measured, never more than the stick asks, never less than the target). false = the old law.

## Bench (regression set, 20 cases, uncalibrated airframe: direction only), 0.7.0 -> 0.7.1
- Mean sideslip 8.90 -> 8.73 deg, mean roll 48.4 -> 48.3 deg/s.
- Push-then-pull-through + roll 450: 16.6 -> 14.5 deg; roll-then-pull reversal 400: 8.4 -> 7.6. No other case moves by more than 0.7 deg.

## Also in the code, off: `ReachHeldStick` (4. Gains, dev)
The allocator's reach model on the stabilators with the pitch stick held (see Revoker_FCS_1.8.1_notes: it calms the Revoker's canards with TVC off, at a cost in pushovers under roll). Shared code, default false, **not benched on this jet**.
