# Su-30VXN FCS 0.7.0: roll cap fix, pitch-rate budget on the AoA rate

Base: 0.6.6. **Status:** compiled against the game's assemblies, benched (regression set on a re-extracted, uncalibrated bench airframe, so read the direction, not the numbers). **Not flown.**

Tom (24 Nov 12:57): "also port to vortex and vortex family, flanker, ifrit, ifrit mtd".

## Changes (shared code, the same text as the Revoker 1.8.0 and J-20B 0.2.0)
- **Roll cap fix (`RateCapFromHold`, 6. Lateral governor).** The roll-rate cap reads its yaw capacity from the hold allocation (the surface positions that make zero angular acceleration at the present rates) instead of from this tick's command. The command followed the roll target, so the old read fed back into the cap (Revoker 1.7.0 / F-22E 4.7.0). false = the law before it.
- **Pitch-rate budget (`RateBudgetOnAoARate`, 2. Envelope).** Full aft stick asks for the pitch-rate budget (speed and thrust based). The budget bounded the whole body pitch rate, flight-path rate included. Rolling inverted over the top of a slow loop, the flight path alone turns at (n + 1) g / V, more than the budget, so the AoA sank with the stick on the stop. The budget now never holds the AoA rate (body rate less flight-path rate) below 6 deg/s (`AuthRateMin`, the floor the authority rate limit already keeps), in the rate limits and in the rate law's stick map. The G, AoA and capture limits still apply. false = the old law.
- **`LateralReserveBacked`** (the Revoker 1.8.0's law for rolling with no nozzle moment) is in the shared code, off by default. It does nothing on this jet either way, see below.

## What changes on this jet
- The roll cap fix **replaces 0.6.6's half-span read** (`RollRateCapSpan` is gone, as on the Revoker 1.7.0). `RateCapFromHold` false gives the law before 0.6.6, not 0.6.6.
- `LateralReserveBacked` does nothing here whichever way it is set: the canards are a pitch-only pair and back the whole reserve. This tree has no plan-hold rule (F-22E 4.5.3 was never ported), so there is no `PlanHoldNozzleShare` key.

## Bench (regression set, 20 cases), 0.6.6 -> 0.7.0
- Mean sideslip 8.56 -> 8.90 deg, mean roll 47.0 -> 48.4 deg/s.
- **Better:** full aft + roll + reversal 500: 14.4 -> 10.8 deg; push-then-pull-through + roll 450: 18.2 -> 16.6; held 0.4 + roll 350: roll 83 -> 92 deg/s at 8.7 -> 8.3 deg.
- **Worse:** full aft + roll + reversal 320: 10.4 -> 14.0 deg; push + roll corkscrew 170: 3.7 -> 6.0; full aft + roll 250: 9.0 -> 10.4.
- The airframe file was rebuilt from `SolarSu30_1.0.1.dll` without the earlier calibration, so do not trust the absolute numbers. Both builds ran on the same file.

