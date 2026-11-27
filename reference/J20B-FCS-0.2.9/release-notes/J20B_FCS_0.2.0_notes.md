# J-20B Weilong FCS 0.2.0: AoA limit 45, the AoA holds through rolls and loops, roll cap fix

Base: 0.1.8. **Status:** compiled against the game's assemblies, benched (J-20B bench airframe re-extracted from the mod, your current config). **Not flown.**

## Tom (24 Nov)
- 12:50: "same thing happened to the j-20, i set aoa limit to 45 towards the end of the log, and rolling cause aoa to drop to 37, fix that if it is the laws. Then bump to 45 aoa limit if the fix works".
- 12:53: "also apply the roll fix to the j-20".

## The log (J20B_FCS_20261124_124127, 0.1.8, limit set to 45)
- 122-128 s: full aft, slow roll while inverted over the top of a loop, 306 -> 212 km/h. AoA 45.0 -> 37.4 deg over 5 s, then back up as the nose came down. No sideslip to speak of (0.4 deg).
- The pitch-rate command sat exactly on the pitch-rate budget the whole time (15-25 deg/s). Inverted at 60 m/s the flight path alone turns at (n + 1) g / V = 18-24 deg/s. The stick was asking for less than it takes to hold the AoA.
- The budget rises as the AoA falls (it is larger below 30 deg), so the AoA settled where budget = need: 37 deg. It is the law, not authority (32-57 deg/s^2 of nose-up left).
- Same at 143-149 s (350 km/h, 3.4 g): AoA stuck at 41.2-42.0 against the 45 limit.
- This jet has no nozzles, so the thrust term of the budget is zero: 5 + 0.15 x IAS at high AoA.

## Changes
- **Pitch-rate budget (`RateBudgetOnAoARate`, 2. Envelope).** Full aft stick asks for the pitch-rate budget (speed and thrust based). The budget bounded the whole body pitch rate, flight-path rate included. Rolling inverted over the top of a slow loop, the flight path alone turns at (n + 1) g / V, more than the budget, so the AoA sank with the stick on the stop. The budget now never holds the AoA rate (body rate less flight-path rate) below 6 deg/s (`AuthRateMin`, the floor the authority rate limit already keeps), in the rate limits and in the rate law's stick map. The G, AoA and capture limits still apply. false = the old law.
- **Roll cap fix (`RateCapFromHold`, 6. Lateral governor).** The roll-rate cap reads its yaw capacity from the hold allocation (the surface positions that make zero angular acceleration at the present rates) instead of from this tick's command. The command followed the roll target, so the old read fed back into the cap (Revoker 1.7.0 / F-22E 4.7.0). false = the law before it.
- **`AoALimitPositive` 40 -> 45** (moved once, only from the old default; `J20ConfigRevision` 5 -> 6). Yours is already 45.
- **In the code, off by default on this jet:** `LateralReserveBacked` and `PlanHoldNozzleShare` (on by default on the Revoker, where they fix the AoA dip with TVC off). They free the pitch axis under roll; on this jet's bench that raised the sideslip in fast pull-throughs under full roll. Turn both on in the dev config to try them, e.g. for flying with TVC off. On this jet the AoA sag was the budget alone: with those two off the bench holds 45 just as well.

With `RateBudgetOnAoARate` and `RateCapFromHold` false the bench log is identical to 0.1.8 (396 columns).

## Bench at the 45 deg limit, 3000 m, full throttle: 0.1.8 -> 0.2.0
| Case | AoA band 0.1.8 | AoA band 0.2.0 | Roll in 3 s (deg) | Sideslip (deg) |
|---|---|---|---|---|
| Full aft + full roll, 250 km/h | 43.7-45.1 | **44.9-45.2** | 28 -> 29 | 3.0 -> 4.0 |
| same, 400 km/h | 42.3-45.3 | **44.9-45.1** | 37 -> 36 | 5.4 -> 3.1 |
| same, 600 km/h | 43.4-45.2 | **44.7-45.3** | 52 -> 54 | 3.5 -> 3.3 |
| After a roll reversal, 250 / 400 / 600 | 38.6 / 38.8 / 39.9 lowest | **44.9 / 44.9 / 44.8** | | 8.6 / 8.5 / 9.9 -> 7.0 / 8.8 / 9.5 |
| Loop with a slow roll over the top (the log's manoeuvre), 400 km/h entry | 38.7-45.2 | **44.9-45.1** | | 5.5 -> 5.6 |
| Full aft + roll, 220 km/h | 43.1-45.2 | **44.9-45.1** | 26 -> 26 | 4.5 -> 3.3 |

- **Pull to 45:** 30 -> 44 deg in 0.90 / 0.70 / 0.87 s at 250 / 400 / 600 km/h -> 0.88 / 0.70 / 0.85 s. Peak 45.1-45.3, holds 44.9-45.0.
- **Roll at the limit is slower at 45 than at 40:** 29 / 36 / 54 deg in 3 s against 43 / 52 / 68 at 40 deg (0.1.8). That is the higher AoA, not the law.
- **Mid-AoA rolls (the roll cap fix):** 550 km/h at 12 deg: 462 -> 527 deg in 2.9 s, the largest drop in the roll curve 114 -> 76 deg/s, sideslip 4.4 -> 5.8. 400 km/h at 18 deg: 202 -> 204 deg, drop 32 -> 28 deg/s.
- **Worse:** push-then-pull-through under full roll at 350 km/h (1500 m): sideslip 7.0 -> 12.2 deg (the roll cap fix; with `RateCapFromHold` false it is 7.0).
- The same manoeuvre at 450 km/h is bad in both builds: 27.5 -> 23.2 deg of sideslip at 130-170 deg/s of roll. Not addressed here.

## Bench notes
- The J-20B bench airframe is not in the source zip (it is derived from the mod's bundle). Rebuilt with `bench/extract/j20_extract.py` from `J-20B Weilong_0.0.16.nobp` + the runtime contract inside `Reasonix.J20.AeroControl.dll`.
- A free replay of the log's loop does not track the game (the bench jet runs out of speed going up), so the loop case above is a bench manoeuvre flown the same way. It reproduces the sag: 45 -> 38.7 deg with the command on the budget.
