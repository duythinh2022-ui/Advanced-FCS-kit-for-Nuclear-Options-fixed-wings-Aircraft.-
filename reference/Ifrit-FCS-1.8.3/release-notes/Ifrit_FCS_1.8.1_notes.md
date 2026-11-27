# KR-67 Ifrit FCS 1.8.1: pull trade against the roll the jet has

Base: 1.8.0. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 13:48): "fix" (the open list of the last builds), 13:49: "no ontop fixes".

## Change (shared code; found on the J-20B, see J20B_FCS_0.2.1_notes)
- **Pull trade against the roll the jet has (`TradeRollHave`, 6. Lateral governor).** Pulling or pushing under a roll, the trade lowers the roll target and gives the rest of the yaw shortfall to the AoA rate. That AoA rate was reckoned against the roll target the trade had just set, as if the roll were already there. The roll takes 0.4-0.6 s to come down to it, and meanwhile the AoA swept at the rate that suits the slower roll. It is now reckoned against the roll the jet has (measured, never more than the stick asks, never less than the target). false = the old law.

## Off by default on this jet
- With it on, the regression set (20 cases) is slightly worse: mean sideslip 4.85 -> 4.95 deg; full aft + roll + reversal 500: 6.8 -> 8.2; push-then-pull-through + roll 450: 5.4 -> 6.2. Full aft + roll 250 reaches 47.3 deg instead of 51.1.
- With it off (the default) 1.8.1 flies exactly as 1.8.0 and 1.7.1 on the bench.
- It does not rescue the two pitch-side laws either (`LateralReserveBacked` + `PlanHoldNozzleShare` on: mean 5.52 -> 5.66). They stay off.

## The 70 -> 68 deg dip when rolling at the limit (MTD): not fixed
The bench still does not reproduce it, so nothing could be checked against it. The 00:05 log's own numbers (the earlier findings doc): AoA command 69.0-69.6, 9-15 of 90 deg/s^2 of nose-up used, sideslip swinging -1.6 -> +2.9 deg at the roll entry with 150-170 deg/s^2 of yaw unmet. None of this session's laws acts there: the pitch-rate command is not on the budget at that speed and thrust, and there is no pitch sweep for the trade. Note that the game had Ifrit 1.5.0 installed since 01:46; that log was 1.7.0.

## Also in the code, off: `ReachHeldStick` (4. Gains, dev)
The allocator's reach model on the stabilators with the pitch stick held (see Revoker_FCS_1.8.1_notes: it calms the Revoker's canards with TVC off, at a cost in pushovers under roll). Shared code, default false, **not benched on this jet**.
