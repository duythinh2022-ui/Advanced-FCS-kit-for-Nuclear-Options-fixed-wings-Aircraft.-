# T/A-30 Compass FCS 0.7.0: the current governor and the roll cap fix

Base: 0.6.0. Minor bump: law change. **Status:** compiled against the game's assemblies, benched (regression set, your config). **Not flown.**

Tom (24 Nov 13:30): "also apply the latest governor and the roll cap fix to the compass".

## Changes
- **F-22E 4.5.2 `PaceRollSatDelivered`:** while the allocator reports the roll saturated, the pace's roll capacity is the delivered one, not the remembered floor.
- **F-22E 4.5.3 `LatShareNoGate`:** the lateral stab share follows the lateral stick alone (it was gated on the pitch stick being held), and the released-pitch priority yields to it.
- **F-22E 4.5.3 `PlanFollowsJet`:** a braking pitch plan stays within 0.1 s of its own deceleration (at most 8 deg/s) of the jet's rate. New log column `planHeld`.
- **Roll cap fix (`RateCapFromHold`, 6. Lateral governor).** The roll-rate cap reads its yaw capacity from the hold allocation (the surface positions that make zero angular acceleration at the present rates) instead of from this tick's command. The command followed the roll target, so the old read fed back into the cap (Revoker 1.7.0 / F-22E 4.7.0). false = the law before it.
- **Pitch-rate budget (`RateBudgetOnAoARate`, 2. Envelope).** Full aft stick asks for the pitch-rate budget (speed and thrust based). The budget bounded the whole body pitch rate, flight-path rate included. Rolling inverted over the top of a slow loop, the flight path alone turns at (n + 1) g / V, more than the budget, so the AoA sank with the stick on the stop. The budget now never holds the AoA rate (body rate less flight-path rate) below 6 deg/s (`AuthRateMin`, the floor the authority rate limit already keeps), in the rate limits and in the rate law's stick map. The G, AoA and capture limits still apply. false = the old law.
- **In the code, off by default on this jet:** `LateralReserveBacked` and `PlanHoldNozzleShare` (on by default on the Revoker, where they fix the AoA dip with TVC off). They free the pitch axis under roll; on this jet's bench that raised the sideslip in fast pull-throughs under full roll. Turn both on in the dev config to try them, e.g. for flying with TVC off.

With every new switch off the bench log is identical to 0.6.0 (394 columns).

## Bench (regression set, 20 cases), 0.6.0 -> 0.7.0
- Mean sideslip 4.01 -> 4.08 deg, mean roll 79.7 -> 81.3 deg/s.
- **Roll up:** pull-then-roll 320 / 400: 38 / 46 -> 42 / 49 deg/s; push-then-pull-through + roll 350 / 450: 121 / 142 -> 129 / 147; full aft + roll + reversal 320: 56 -> 59.
- **Sideslip:** pull-then-roll 400: 3.7 -> 4.3 deg; roll-then-pull reversal 400: 4.9 -> 5.1; pull-through 350 / 450: 6.2 / 8.6 -> 6.4 / 8.8; 65 AoA roll + yaw release: 14.2 -> 13.8. The rest within 0.2.

## Seen on the way, not changed
The Compass lets the AoA fall a long way under full roll at full aft: 18 deg at 2 s into the roll against the 35 deg limit (full aft + roll 250), 13 deg in the roll-then-pull reversal. It has no nozzles, so the lateral reserve hands the tail's pitch authority to the roll with nothing behind it, the Revoker's TVC-off case. With `LateralReserveBacked` and `PlanHoldNozzleShare` on, the same cases hold 21-31 deg, the mean roll drops 79.7 -> 73.8 deg/s and the mean sideslip is 4.20 deg. Left off because you asked for the governor and the roll cap fix; say if you want it on.
