# F-22E FCS 4.8.1: two-zone pitch authority and the low-AoA reverse-roll stab pose

Base: 4.8.0 as installed. Only `TwoZoneAirframe = true` behaves differently; with it off (X2e) the bench output is identical to 4.8.0 apart from three new log columns.
Bench only, single runs except where noted. Not flown.

## From Tom's 21:44 flight (two-zone on, TVC off 40-180 s, on after)
- **Pitch.** The FCS's measured nose-down margin per unit dynamic pressure, compared with the X2e flight at 21:04:
  - 10° AoA: 10.4 against 24.5;
  - 25° AoA: 2.5 against 16.5;
  - 30-35° AoA: 4.6-4.9 against 10.5-12.3.
  - Below about 300 km/h the swept authority cap came down to about 28°, then rose at 1°/s with the jet sitting on it. This is the "won't pull past the 40s".
- **Reverse-roll stab pose.** The stabs were in it in 58 % of the TVC-on rolling rows at 3-22° AoA (25 % with TVC off; X2e flight: 0):
  - both stabs trailing-edge down (sym -17 to -21);
  - one stab differential the wrong way;
  - the nozzles pitching up against them.

## Causes (bench, full two-zone set)
1. **Pitch: the 25 Nov refit of the fore/aft lift schedules.** It cuts the aft-body lift to 0.31-0.88 x at 4-40° AoA.
   - X2e with only these two schedules swapped in reproduces the whole margin loss.
   - The outboard stab zone on its own does not change the margin.
   - With the full two-zone set, the refit takes 40-55 % of the nose-down margin at 15-40° AoA.
2. **Stab pose: a stab driven trailing-edge down past the outboard zone's lift peak.**
   - The outboard zone loses lift while the inboard zone still gains it.
   - The outboard zone acts 0.7 m further out, so the stab rolls the wrong way.
   - At low AoA the allocator parked a stab there.

## Changes
- **`TwoZoneLiftRefit`** (dev, 5. Airframe, default 0.25, applied at spawn). Sets how much of the refit is used: 0 = X2e's schedules, 1 = 4.8.0.
- **`TwoZoneStabPeakClamp`** (dev, 5. Airframe, default true, takes effect at once).
  - Below 20° AoA, each stab's travel keeps its outboard zone within its lift peak + 3° of local AoA.
  - The limit opens up between 20 and 30° AoA, so the high-AoA inside-stab-down onset is untouched.
- **Log columns:**
  - `authFail`: the first AoA where the swept margin fails (-1 = none);
  - `authMargAt`: the swept nose-down margin at the jet's AoA;
  - `authFromAbove`: unused switch, -1.

## Bench, 4.8.1 two-zone (refit 0.25 + clamp) against X2e and 4.8.0 two-zone

Nose-down margin per kPa at 330 km/h, TVC off:

| AoA | 10 | 15 | 20 | 25 | 30 | 35 | 40 | 45 | 50 | 55 |
|---|---|---|---|---|---|---|---|---|---|---|
| X2e | 28.1 | 24.5 | 23.0 | 22.0 | 20.5 | 19.1 | 18.1 | 19.3 | 14.2 | 13.0 |
| 4.8.0 two-zone | 21.2 | 14.7 | 12.3 | 11.3 | 11.5 | 13.5 | 15.0 | 17.6 | 19.5 | 18.5 |
| 4.8.1 two-zone | 26.8 | 21.7 | 20.2 | 19.9 | 19.5 | 19.5 | 19.3 | 20.7 | 18.3 | 18.1 |

TVC off:

| Case | X2e | 4.8.0 two-zone | 4.8.1 two-zone |
|---|---|---|---|
| Push from 28°, 450 km/h, pitch rate after 1 s | -74 | -46 | -73 |
| Push in a roll, 36 / 45°, 1 s | -26.7 / -20.0 | -16.7 / -15.2 | -25.0 / -21.5 |
| Roll at 17° AoA, 450 km/h | 117 | 81 | 122 |
| Reversal roll rate, 30 / 36 / 45 / 50° | 48 / 37 / 31 / 27 | 44 / 46 / 46 / 51 | 65 / 55 / 55 / 52 |
| Stab trim in the 30-50° holds | +2 to +7 | +1 to +10 | +9 to +20 (TE up) |

TVC on:

| Case | X2e | 4.8.0 two-zone | 4.8.1 two-zone |
|---|---|---|---|
| Reversal roll rate, 30 / 36 / 45 / 50° | 72 / 61 / 45 / 39 | 69 / 66 / 56 / 51 | 78 / 70 / 57 / 51 |
| Push in a roll, 36 / 45°, 1 s | -47.8 / -47.3 | -39.8 / -40.9 | -45.8 / -41.0 |
| Roll at 17-19° AoA, 450 km/h | 126 | 111 | 130 |

## Reverse-pose bench case
TVC on: full aft, full right roll from 3 s, pitch released at 5 s, roll held to 9 s. Six speeds, 300-425 km/h, at refit 0.25.

| | Speeds trapped | Reverse-pose rows, trapped speeds | Roll rate at 9 s, trapped speeds |
|---|---|---|---|
| Clamp off | 3 of 6 (325 / 350 / 375) | 64 / 60 / 67 | 96-97 deg/s |
| Clamp on | none | 11 / 0 / 9 (short, at most 5° and 10° of differential) | 101-111 deg/s |

- The clamp cost at 17° AoA is within 2 %.
- At refit 1.0 (4.8.0) the same case did not trap on the bench. The game did, so the bench catches this case only at some states.

## Cost: more sideslip in rolls at 36°
Five-member ensembles, 300-340 km/h. Two cases:
- slow: idle, full aft at 36°, full roll;
- pull: 430-470 km/h, roll, then full aft.

| | Slow: worst sideslip / roll rate | Pull: worst sideslip |
|---|---|---|
| X2e | 1.4-1.8° / 23-26 | 3.3-4.0° |
| Refit 1.0 (4.8.0) | 2.5-3.2° / 29 | 3.0° |
| Refit 0.5 | 4.7-7.3° | 4.8-6.8° |
| Refit 0.25 (default) | 6.0-9.0° / 35-40 | 6.9-9.9° |
| Refit 0.25 with `TwoZoneRollReserve = 0` (swept roll limit) | 3.9-4.6° / 30-32 | not run |

- The restored margin is partly spent by the measured-reserve roll limit on roll rate, and the sideslip follows.
- The clamp does not change these numbers.

## Not verified
- Not flown.
- The game-side cap drop (to about 28° on two-zone, 39° on X2e, below 300 km/h) does not happen on the bench in either airframe. The new log columns are there to show the margin the game's sweep computes.
- Take-off, landing and cruise trim not run on any two-zone setting.
- The two-zone parts are invented to fit F-35 footage. No source.
