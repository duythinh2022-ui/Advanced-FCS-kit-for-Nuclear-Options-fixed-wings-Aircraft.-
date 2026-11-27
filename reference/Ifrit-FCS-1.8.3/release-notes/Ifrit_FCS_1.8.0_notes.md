# KR-67 Ifrit FCS 1.8.0: roll cap fix, pitch-rate budget on the AoA rate

Base: 1.7.1. **Status:** compiled against the game's assemblies, benched (regression set, your config; the same DLL flies the Multirole MTD). **Not flown.**

Tom (24 Nov 12:57): "also port to vortex and vortex family, flanker, ifrit, ifrit mtd".

## Changes (shared code, the same text as the Revoker 1.8.0 and J-20B 0.2.0)
- **Roll cap fix (`RateCapFromHold`, 6. Lateral governor).** The roll-rate cap reads its yaw capacity from the hold allocation (the surface positions that make zero angular acceleration at the present rates) instead of from this tick's command. The command followed the roll target, so the old read fed back into the cap (Revoker 1.7.0 / F-22E 4.7.0). false = the law before it.
- **Pitch-rate budget (`RateBudgetOnAoARate`, 2. Envelope).** Full aft stick asks for the pitch-rate budget (speed and thrust based). The budget bounded the whole body pitch rate, flight-path rate included. Rolling inverted over the top of a slow loop, the flight path alone turns at (n + 1) g / V, more than the budget, so the AoA sank with the stick on the stop. The budget now never holds the AoA rate (body rate less flight-path rate) below 6 deg/s (`AuthRateMin`, the floor the authority rate limit already keeps), in the rate limits and in the rate law's stick map. The G, AoA and capture limits still apply. false = the old law.
- **In the code, off by default on this jet:** `LateralReserveBacked` and `PlanHoldNozzleShare` (on by default on the Revoker, where they fix the AoA dip with TVC off). They free the pitch axis under roll; on this jet's bench that raised the sideslip in fast pull-throughs under full roll. Turn both on in the dev config to try them, e.g. for flying with TVC off.

## Bench (regression set, 20 cases, TVC on), 1.7.1 -> 1.8.0
- **Identical to 1.7.1 in all 20 cases** with the default keys (mean sideslip 4.85 deg, mean roll 50.0 deg/s). Neither change acts in these cases on this jet (why was not looked into).
- With `RateCapFromHold` and `RateBudgetOnAoARate` false the bench log is identical to 1.7.1 (387 columns).

## Why the two pitch-side laws are off here (both on, same set)
| Case | Sideslip 1.7.1 -> both on (deg) | Mean roll (deg/s) |
|---|---|---|
| push-then-pull-through + roll 350 | 6.2 -> **13.6** | 51 -> 41 |
| push-then-pull-through + roll 450 | 5.4 -> **10.8** | 82 -> 70 |
| pull-through, roll released at zero 350 | 6.6 -> 9.2 | 22 -> 27 |
| held 0.4 + roll 350 | 7.7 -> 5.5 | 92 -> 76 |
| roll-then-pull reversal 400 | 5.7 -> 4.5 | 53 -> 52 |
| mean of 20 | 4.85 -> 5.52 | 50.0 -> 46.8 |

- At 410 km/h the nozzles (at their 15 deg stop) back only 0-30 % of the reserve on this jet, so the pitch axis is freed almost as with TVC off. The pull-through then sweeps the AoA at up to 75 deg/s (was 44-51) under 100 deg/s of roll and the V-tail's yaw cannot follow.
- Each law alone: reserve only 12.9 / 13.0 deg, plan hold only 10.6 / 9.4 deg at 350 / 450.
- Your 00:05 report (AoA 70 -> 68 when rolling at the limit, MTD) is not addressed by this build. The bench still does not reproduce that dip.

## MTD
The same DLL. Not benched separately for this build.

