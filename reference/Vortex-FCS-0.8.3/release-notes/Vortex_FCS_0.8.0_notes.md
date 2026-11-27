# FS-20 Vortex FCS 0.8.0: roll cap fix, pitch-rate budget on the AoA rate

Base: 0.7.3. **Status:** compiled against the game's assemblies, benched (regression set, your config; bench solves in C#, the same laws as the native core). **Not flown.**

Tom (24 Nov 12:57): "also port to vortex and vortex family, flanker, ifrit, ifrit mtd".

## Changes (shared code, the same text as the Revoker 1.8.0 and J-20B 0.2.0)
- **Roll cap fix (`RateCapFromHold`, 6. Lateral governor).** The roll-rate cap reads its yaw capacity from the hold allocation (the surface positions that make zero angular acceleration at the present rates) instead of from this tick's command. The command followed the roll target, so the old read fed back into the cap (Revoker 1.7.0 / F-22E 4.7.0). false = the law before it.
- **Pitch-rate budget (`RateBudgetOnAoARate`, 2. Envelope).** Full aft stick asks for the pitch-rate budget (speed and thrust based). The budget bounded the whole body pitch rate, flight-path rate included. Rolling inverted over the top of a slow loop, the flight path alone turns at (n + 1) g / V, more than the budget, so the AoA sank with the stick on the stop. The budget now never holds the AoA rate (body rate less flight-path rate) below 6 deg/s (`AuthRateMin`, the floor the authority rate limit already keeps), in the rate limits and in the rate law's stick map. The G, AoA and capture limits still apply. false = the old law.
- **In the code, off by default on this jet:** `LateralReserveBacked` and `PlanHoldNozzleShare` (on by default on the Revoker, where they fix the AoA dip with TVC off). They free the pitch axis under roll; on this jet's bench that raised the sideslip in fast pull-throughs under full roll. Turn both on in the dev config to try them, e.g. for flying with TVC off.

## Bench (regression set, 20 cases), 0.7.3 -> 0.8.0
- Mean sideslip 3.44 -> 3.60 deg, mean roll 41.2 -> 41.5 deg/s.
- **Better:** full aft + roll + reversal 500: 5.3 -> 4.8 deg; pull-then-pushover + roll 450: 6.4 -> 5.7; push-then-pull-through + roll 350: 4.8 -> 4.3.
- **Worse:** roll-then-pull reversal 400: 3.0 -> 4.1 deg; push + roll corkscrew 170 km/h: 1.7 -> 3.1; pull-then-pushover + roll 350: 4.3 -> 4.8.
- With `RateCapFromHold` and `RateBudgetOnAoARate` false the bench log is identical to 0.7.3 (456 columns).

## Why the two pitch-side laws are off here (both on, same set)
Mean sideslip 3.44 -> 4.12 deg. Full aft + roll + reversal 500: 5.3 -> 7.5; push + roll 300: 3.2 -> 5.8; pull-then-pushover 450: 6.4 -> 8.1; pull-throughs 350 / 450: 4.8 / 5.2 -> 7.2 / 7.4.

## Vortex Family FCS 0.8.0
Built from the same tree (same family fingerprint), so the family jets fly the same laws. STOVL laws untouched. Not benched separately.

## Not in this build
`Vortex-FCS-0.8.0-github.zip` (the 0.7.3 folder had one): the GitHub release is yours to make.

