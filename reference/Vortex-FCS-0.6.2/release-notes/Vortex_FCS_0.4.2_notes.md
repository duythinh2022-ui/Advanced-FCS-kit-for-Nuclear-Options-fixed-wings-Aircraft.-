# FS-20 Vortex FCS 0.4.2 (17 Nov 2026)

On 0.4.1 (the airbrake fix, the hover hand-overs 20 kt later and the ship-relative switch from the other chat, all carried unchanged). STOVL hover only. **Benched, not flown.**

Tom, 21:06, on the four items listed as worse in 0.4.0: "why worse, fix them".

Three builds are compared below. The hover cases are identical in 0.4.0 and 0.4.1 (checked, every column), so the middle column stands for both.

## Result

| # | Item (bench) | 0.3.15 | 0.4.0 / 0.4.1 | 0.4.2 |
|---|---|---|---|---|
| 1 | Full-stick side stop, Buffed 11.9 t: speed past zero | 0.15 m/s | 0.24 | **0.14** |
| 1 | Same stop: time to stay under 0.15 m/s | 3.95 s | 5.10 | **4.10** |
| 2 | 3 m/s gust from a held spot: ends | 2.7 m off | 5.0 m off | **on the spot** |
| 3 | Side + fore-aft together, height lost: Stock 12.8 t | 0.08 m | 0.37 | **0.06** |
| 3 | Same: Stock 11.9 t / Buffed 13.6 t | 0.11 / 0.08 | 0.21 / 0.15 | **0.06 / 0.09** |
| 4 | 500 kg on one wing, full-stick stop to 0.3 m/s | 6.68 s | 7.65 | **7.27** (not back to 6.68) |

Items 1 to 3 are fixed. Item 4 is half recovered, and section 4 says why the rest stays.

## 1. Side stop past zero

**Why.** The side-velocity stop plans when the bank has to come out, and leaves a margin for the bank's lag behind its target. Two numbers have to agree and did not:
- `StovlTrcLagMargin`: 0.32 s since 0.3.9 (the 20 % cut of the start and brake margin).
- The bank loop: 2.5 / s, which trails a target moving at 10 deg/s by 4°, that is 0.40 s.

So the bank came out 0.08 s later than planned, and the jet was still braking as it passed zero. 0.3.15 hid part of this: its position hold took a spot at 3 m/s, ahead of the jet, and pulled it forward through the end of the stop. That same early catch is what slid a short side step back 0.7 m. 0.4.0 moved the catch to 0.5 m/s, which cured the slide and uncovered the overrun.

**Fix.** `StovlTrcLagFromLoop` (true): the profile's margin is never less than the bank loop's own lag (1 / gain = 0.40 s). `StovlTrcLagMargin` stays in the file at 0.32 and does nothing below 0.40.

| Buffed 11.9 t, full stick 5 s, released | 0.3.15 | 0.4.0 | 0.4.2 |
|---|---|---|---|
| Speed past zero | 0.15 m/s | 0.24 | 0.14 |
| Time to stay under 0.15 m/s | 3.95 s | 5.10 | 4.10 |
| Time to 0.3 m/s | 3.83 s | 3.85 | **3.97** |
| Height | −0.00 / +0.03 m | −0.02 / +0.02 | −0.02 / +0.02 |
| Heading | ±0.1° | ±0.1° | −0.1 / +0.3° |
| Short side step: slide back / settled | 0.69 m / 5.9 s | 0.00 / 4.8 | 0.00 / **3.9** |

**Cost:** the stop to 0.3 m/s is 0.12 s longer. That is what the 0.3.9 cut bought.

The other jets (speed past zero, 0.3.15 → 0.4.0 → 0.4.2): 20 % fuel 0.23 → 0.29 → 0.14 (settled 5.2 → 8.1 → 5.3 s); Stock 0.04 → 0.17 → 0.10; 13.6 t 0.10 → 0.19 → 0.11; 12.8 t 0.00 → 0.10 → 0.04.

**The other way, built and off by default:** `StovlBankGainFromMargin` runs the bank loop at 1 / margin (3.1 / s) so the loop matches the 0.32 s instead. Bench, same stop: 0.11 m/s past zero, 3.88 s to 0.3 m/s. But the loop then asks for 31 deg/s² at every corner of the bank target and the roll budget is 24:
- roll posts at their stop for 0.72 s (0.22 s);
- heading ±1.1° in the stop (±0.1°), **−4.5° on a short side step** (−1.8°), with the yaw nozzle at its stop for 0.47 s;
- height −0.04 m (−0.02).

**Tried first, removed:** feeding the bank loop its target's own rate. The target's slope steps at every corner of the profile, so the posts went to their stops there. Height −0.09 / +0.07 m.

## 2. Gust

**Why.** The position hold let go of its spot above 3 m/s. A 3 m/s gust was just over that, so the spot was lost. 0.3.15 then took a new one at once (2 m back toward the old one, by its rest-point estimate); 0.4.0 took one where the jet stopped.

**Fix.** `StovlPosKeepSpeed` (0 = no limit; 3 = before). A spot once held is kept whatever the speed. Only the lever, the lateral stick or the wheels give it up. The way back is flown at 2 m/s at most, as before.

| 3 m/s sideways kick, ends | 0.3.15 | 0.4.0 | 0.4.2 |
|---|---|---|---|
| Buffed 11.9 t | 2.7 m off | 5.0 | 0.0 |
| Stock / 20 % fuel / 12.8 t / 13.6 t | 3.4 / 2.7 / 3.8 / 3.1 | 5.2 / 5.2 / 6.0 / 0.0 | 0.0 / 0.0 / 0.1 / 0.0 |
| 500 kg on one wing | 5.0 | 8.4 | 1.1 and still closing when the 15 s case ends |

Heading in the gust on the Buffed jet: +0.4° (was +1.4°).

## 3. Height in a fore-aft stop with little spare lift

**Why.** The regression was not in the combined move. 0.4.0's smaller side command left the fore-aft stop its whole budget, and the fore-aft stop alone already lost 0.45-0.54 m at 12.8 t in every build. The trace of that stop (Stock 12.8 t, 0.47 m/s² of spare lift, from 14 kt):

1. In the hover the pitch moment is the lift fan's pitch term, and every newton of that is lift. Starting or stopping a pitch rate moves the vertical speed by a fixed amount per deg/s (about 0.025 m/s per deg/s here), however it is done, unless the thrust makes it up.
2. The nose-up tilt started at 10 deg/s. The fan added 3.8 kN, and the thrust was cut 10 % to hold the height (throttle 0.72 → 0.51).
3. 0.5 s later the tilt was stopped: 8.6 kN less fan (0.67 m/s²) with the engine still spooling back up. Throttle at its stop for 1 s, −0.35 m/s.
4. The sink took the tilt budget back, the nose came down, the jet recovered, the tilt came in again. **Three rounds, 9.2 s to stop, −0.45 m.**

The 0.2.8 cap on the fan did not stop it: it read the headroom from the thrust of the moment, so the cut in step 2 opened it for step 3.

**Fix.** `StovlTiltLiftLimit` (true). The fore-aft tilt's rate counts only the pitch authority the vertical axis can spare: the fan lift step the thrust can make up, with the thrust rising at (spare lift) / (engine lag), plus whatever pitches the jet without touching the lift. The spare lift is the hover's own, not the thrust of the moment. The rate is still authority × `StovlTiltStopTime` under the 10 deg/s ceiling.

`StovlTiltLiftDip` (0.2 m) is the height the model lets a tilt cost. It is set so the Buffed jet with full fuel keeps its 10 deg/s, the same way `StovlTrcStopTime` was set to keep its 15 kt.

| Jet | Spare lift | Fore-aft tilt rate | Fore-aft stop: height lost, 0.3.15 → 0.4.0 → 0.4.2 | Stop to 0.3 m/s |
|---|---|---|---|---|
| Buffed 11.9 t | 2.75 m/s² | 10 deg/s | 0.09 → 0.08 → 0.08 m | 4.24 → 4.37 → 4.37 s |
| Buffed 20 % fuel | 6.12 | 10 | 0.08 → 0.07 → 0.07 | 4.12 → 4.37 → 4.37 |
| Buffed 500 kg one side | 2.25 | 9.5 | 0.08 → 0.08 → 0.08 | 4.30 → 4.39 → 4.40 |
| Buffed 13.6 t | 1.45 | 7.2 | 0.08 → 0.08 → 0.06 | 4.44 → 4.40 → **4.62** |
| Stock 11.9 t | 1.09 | 5.6 | 0.12 → 0.14 → 0.06 | 4.64 → 4.35 → **4.85** |
| Stock 12.8 t | 0.47 | 3.5 | 0.54 → 0.45 → **0.04** | 8.97 → 9.24 → **4.52** |

Side + fore-aft together, height lost: 12.8 t 0.08 → 0.37 → 0.06 m; Stock 0.11 → 0.21 → 0.06; 13.6 t 0.08 → 0.15 → 0.09; Buffed 11.9 t 0.08 in all three.

**Cost:** the fore-aft stop is 0.2 s longer than 0.3.15 on the Stock jet and at 13.6 t Buffed.

**Still there at 12.8 t:**
- The tilt-in hesitates once: pitch rate 3.3 → 0.4 → 2.8 deg/s, 0.7 s at 3.8° of tilt, when the nozzle reaches its forward stop and the budget steps down. Height within 0.04 m through it.
- The stop runs 0.24 m/s past zero (0.05 in 0.4.0, 0.46 in 0.3.15) and comes back without a net slide.

With `StovlTiltLiftDip` at 0.05 the Buffed 11.9 t jet tilts at 6.5 deg/s: fore-aft stop −0.05 / +0.11 m instead of −0.08 / +0.16, and 0.35 s longer.

## 4. One-sided load: the slower bank

**Why.** 0.4.0 sized the bank rate on (roll authority around the posts' present trim) × √(weak side ÷ that authority). With 500 kg on a wing the posts sit at 79 % and the authority around that trim is already cut by the load (17.6 of 21.2 deg/s²). The load was counted twice: 3.5 deg/s.

**Fix.** `StovlTiltRollFull` (true): the budget is √(weak side's stopping power × the authority over the effectors' whole travel), which is the 0.2.8 rule as written. 4.0 deg/s. On a balanced jet the two forms are the same number.

| 500 kg on one wing, full stick 5 s, released | 0.3.15 | 0.4.0 | 0.4.2 |
|---|---|---|---|
| Bank rate budget | 4.9 deg/s | 3.5 | 4.0 |
| Stop to 0.3 m/s | 6.68 s | 7.65 | 7.27 |
| Speed past zero | 0.51 m/s | 0.38 | 0.26 |
| Slide back | 0.66 m | 0.44 | 0.44 |
| Time to stay under 0.15 m/s | 9.93 s | 10.70 | 10.37 |
| Posts at their stop | 1.70 s | 0.50 | 0.78 |

**Why not 6.68 s.** The weak side stops a roll with 4.3 deg/s². 0.3.15's 4.9 deg/s took that side 1.15 s to stop, and that is where its 0.51 m/s overrun, 0.66 m slide and 1.7 s of post saturation came from. By the rule the rate the weak side can stop in its stretched stop time is 4.0. A short side step on this jet still slides back 1.73 m (1.84 in 0.4.0, 2.63 in 0.3.15); not looked into.

## 5. Other changes seen (0.4.1 → 0.4.2)

- **30 kt bank stick** (hover law since 0.4.1): bank 10.2 → 9.6°, sideslip 9.1 → 8.8° on the Buffed jet; within 0.8° on the others.
- **Deceleration to the hover:** time to under 1 kt within 0.3 s on every jet; the last stop at 12.8 t moves the height ±0.02 m (was −0.05 / +0.06).
- **Vertical landings:** gear compression 0.419 m at most (0.418; a leg breaks at 0.5).
- **Climb / sink release, short take-off, pedal turns:** same to the last digit shown.
- **Curve checks** (reversals of roll rate, pitch rate, bank command, nozzle, throttle, vertical speed): no counter differs from 0.4.1 on any of the six jets.

## 6. Checked

- Every 0.4.2 switch off: identical to the 0.4.1 release build on the reference jet's five cases, every column, largest difference 0. Earlier in the same work, before the rebase: every switch off identical to 0.3.15.
- CTOL (pull + roll at 400 and 700 km/h): identical to the 0.4.1 release build on every flight column (only the code timers differ).
- The toolchain rebuilds the shipped 0.4.1 DLL byte for byte from its source; the 0.4.2 source zip rebuilds the 0.4.2 DLL byte for byte.

## 7. Not benched

2 000 m, damage, other store loads, ship-relative mode (the bench has no ship), the game itself.

## 8. Dev keys (9. STOVL) and log columns

| Key | Default | Off / before |
|---|---|---|
| `StovlTrcLagFromLoop` | true | false |
| `StovlBankGainFromMargin` | false | false |
| `StovlPosKeepSpeed` | 0 (no limit) | 3 |
| `StovlTiltLiftLimit` | true | false |
| `StovlTiltLiftDip` | 0.2 m | (not used when the limit is off) |
| `StovlTiltRollFull` | true | false |

New log columns after `stWz`: `stTiltPitchA` (the pitch authority the fore-aft tilt plans on, deg/s²), `stKBank` (the bank loop's gain).

Bench: `bench/scripts/vortex/matrix42.sh` (six jets, 0.4.1 against 0.4.2), `hrun.sh` (the hover case alone), `hm.py`, `yaw.py`, `final3.py`, `dend3.py`.
