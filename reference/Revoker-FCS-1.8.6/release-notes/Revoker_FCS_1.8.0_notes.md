# FS-12 Revoker FCS 1.8.0: the AoA holds while rolling at the limit with no nozzle moment

Base: 1.7.1. Minor bump: law change. **Status:** compiled against the game's assemblies, benched (replays of the 24 Nov 12:06 and 12:32 flights, the regression set with TVC off, the full suite with TVC on). **Not flown.**

## Tom (24 Nov)
- 12:38: "rolling still cause aoa to dip".
- 12:50: "fix it no on top fixes. same thing happened to the j-20 ... fix that if it is the laws."

## What was wrong (TVC off, full aft at the 30-45 deg cap, roll entry or reversal)
The game logs show 6-18 deg dips: 34.1 -> 23.3 deg at 442 km/h, 40.2 -> 28.1 at 504 km/h. Three laws, none of them a special case for this:
1. The lateral reserve gave 70 % of the canards' and elevons' pitch authority to the roll with nothing to replace it. With TVC on the nozzles replace it.
2. The pitch plan waited for the jet (F-22E 4.5.3). Without nozzles that only capped the brake demand: the pull to the cap stopped late (33.2 deg at the 30 deg cap), the canards went to 53 deg leading edge down to stop it and were still coming back 0.4 s later, while the nose fell at 91 deg/s^2 against a +12 demand.
3. The pitch-rate budget bounded the flight-path rate too (the J-20B's case; rarely binding on this jet).

## Changes (dev keys; with all three false the replays are identical to 1.7.1, 396 log columns)
- **Lateral reserve backed (`LateralReserveBacked`, 3. Lateral-directional).** `LateralStabReserve` (0.7) hands that share of the surfaces' pitch authority to the roll while roll stick is in. It assumed the nozzles carry the pitch. The share is now no larger than what the effectors that cannot roll can put back: the pitch nozzles at the present thrust and a pitch-only canard pair. One number for all three legs (allocator cost, pitch plan, priority share). false = always the full share.
- **Plan hold by nozzle share (`PlanHoldNozzleShare`, 2. Envelope).** The F-22E 4.5.3 rule "the braking pitch plan waits for the jet" exists for the nozzles. It now waits in proportion to the pitch the nozzles carry (`CapTvcShare`, as the pitch lead and the capture lag already do). With no nozzle moment it only capped the brake demand, so pulls stopped late. false = the full wait.
- **Pitch-rate budget (`RateBudgetOnAoARate`, 2. Envelope).** Full aft stick asks for the pitch-rate budget (speed and thrust based). The budget bounded the whole body pitch rate, flight-path rate included. Rolling inverted over the top of a slow loop, the flight path alone turns at (n + 1) g / V, more than the budget, so the AoA sank with the stick on the stop. The budget now never holds the AoA rate (body rate less flight-path rate) below 6 deg/s (`AuthRateMin`, the floor the authority rate limit already keeps), in the rate limits and in the rate law's stick map. The G, AoA and capture limits still apply. false = the old law.

## Bench: replays of the two TVC-off flights, 1.7.1 -> 1.8.0
| Window | Game: AoA before -> lowest | 1.7.1 | 1.8.0 | Roll at 1 s (deg/s) | Largest drop in the roll curve (deg/s) |
|---|---|---|---|---|---|
| 12:06 137.6 s, 449 km/h, cap 30 | 33.3 -> 23.2 | 32.3 -> 17.4 | **30.6 -> 27.7** | 55 -> 32 | 16.1 -> 1.1 |
| 12:06 106.6 s, 539 km/h, cap 30, reversal | 29.6 -> 24.1 | 29.7 -> 25.4 | **29.7 -> 29.0** | 47 -> 41 | 8.3 -> 1.5 |
| 12:06 42.3 s, 405 km/h, cap 45 | 44.0 -> 35.8 | 44.2 -> 39.5 | **44.2 -> 42.3** | 26 -> 24 | 9.1 -> 3.9 |
| 12:06 254.3 s, 513 km/h, cap 35 | 30.4 -> 16.6 | 30.3 -> 22.9 | **30.5 -> 27.5** | 48 -> 32 | 13.0 -> 6.0 |
| 12:32 43.2 s, 497 km/h, cap 35 | 39.1 -> 28.1 | 40.5 -> 31.2 | **39.5 -> 34.3** | 37 -> 34 | 5.1 -> 1.9 |
| 12:32 63.8 s, 400 km/h, cap 35 | (starts on a roll release) | 34.6 -> 34.7 | 34.0 -> 34.1 | 32 -> 23 | 1.3 -> 2.7 |

- The bench's own 1.7.1 dips are shallower than the game's in three windows, so expect the same direction in the game, not these exact numbers.
- **Cost: roll rate.** With no nozzles the same surfaces do pitch and roll. At a held AoA the roll is 10-30 % slower (last row: 32 -> 23 deg/s at 35 deg with no dip in either build). Part of the old roll rate came from the AoA having dropped to where more roll is available.
- 254 s window: the re-pull overshoots the 35 deg cap by 1.4 deg once (36.4), then holds 35.0-35.5. 1.7.1 never got back above 34.5.

## Bench: regression set with TVC off (cap 45), 1.7.1 -> 1.8.0
- **AoA 2 s into the roll:** pull-then-roll 250 / 320 / 400: 32.1 / 33.9 / 33.8 -> 34.5 / 38.8 / 41.7. Full aft + roll 250: 27.3 -> 33.1. Full aft + roll + reversal 320 / 500: 30.9 / 38.6 -> 38.6 / 41.3. Roll-then-pull reversal 400: 25.5 -> 32.5.
- **Mean roll:** 16 / 22 / 24 -> 13 / 21 / 22; 23 -> 19; 23 / 30 -> 18 / 26; 42 -> 33 deg/s.
- **Sideslip better:** reversals 5.3 -> 4.2 and 7.3 -> 6.4 deg, roll-then-pull reversal 5.8 -> 2.7. The pull-through with the roll released: AoA overshoot 50.9 -> 42.4 deg.
- **Sideslip worse:** push-then-pull-through + roll 350 / 450: 2.9 / 3.9 -> 5.2 / 5.7 deg (the pitch sweeps faster under the roll). Pull-then-pushover + roll 450: 6.0 -> 6.5. Sweep reversal 400 km/h at 14 deg: 3.6 -> 6.4.
- Held rolls, surface travel: full aft 400 km/h 31.2 -> 17.8 deg/s, 460 km/h 18.9 -> 15.3, 300 km/h 23.4 -> 25.6.

## Bench: TVC on (full suite, 123 rows)
- Sideslip within 0.6 deg of 1.7.0 everywhere. At speed the nozzles back the whole share, so the laws act as before.
- Largest changes: full aft + roll + reversal 500 peak roll 113 -> 104 deg/s (mean 34 -> 31); falling-leaf stop / reversal 370 roll 73 -> 64 deg/s.
- Held rolls: surface travel 13.4 / 10.7 / 24.1 -> 13.2 / 10.0 / 17.5 deg/s, roll rates the same.

## Not fixed
- The canards still cross their stall in TVC-off rolls (the visible twitch). 1.7.0 halved it; this build does not target it.
- Full aft with TVC on at 70 deg: unchanged.

## Shared code
The same three laws and the roll cap fix are in the J-20B 0.2.0, Ifrit 1.8.0, Vortex / Vortex Family 0.8.0, Su-30 0.7.0 and Compass 0.7.0. `LateralReserveBacked` and `PlanHoldNozzleShare` are on by default only here. Not in the F-22E yet (4.7.0 has the roll cap fix).
