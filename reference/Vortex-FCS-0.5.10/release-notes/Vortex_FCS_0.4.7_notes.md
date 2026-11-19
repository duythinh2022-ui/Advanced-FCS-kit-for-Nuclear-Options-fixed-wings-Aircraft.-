# Vortex FCS 0.4.7: ship take-off skid, the side-stick pause, pedal turns at speed

18 Nov 2026. Built on 0.4.6 (the airbrake fix 0.4.5 and the ship speed match 0.4.6 from the other chat are carried unchanged). Five changes in the STOVL laws, each behind a Dev switch, plus eight telemetry columns. **Benched, not flown.** The ship and the game's origin shift are not on the bench (the shift is imitated there by moving the jet's position in one tick).

Tom, 17 Nov, on the 22:54, 22:55 and 22:56 logs (flown on 0.4.4, Buffed, 14.19 t / 14.19 t / 13.82 t, `StovlTrcBankRate` 30, `StovlBankGainFromMargin` on, margin 0.32 s, ship-relative on):

> one of the log i took off from a moving ship. The jet skid to one side on takeoff. The other one has side skidding often pauses, like the jet just hard stop at leveled for side skid for a while then it follows my stick command, it should've not done that and follow my command on whatever budget it has left. Also when i yaw left with forward velocity, with no roll input, the jet auto compensate for the generated side skid just fine, but when i put in right roll, at least it should've cancel the side skid or follow that command within the budget

## 1. Ship take-off skid (22:54 log)

- **What the log shows.** Lift-off at 29.8 s from a ship doing 16 m/s. At 31.316 s, in one tick (1/60 s), the position hold's speed command goes 0.09 → 2.00 m/s (its limit) and the acceleration demand 0.15 → 1.73 m/s², with the jet at 0.15 m/s over the deck and no stick input. The jet then flies off at 2.78 m/s with 8.7° of bank until a stick input drops the hold at 36.7 s.
- **Cause.** The game moves its whole world back to the middle once the player is about 1 km from it (`FloatingOrigin`: threshold 1024 m, shift in 64 m steps; read from the game code). Every position jumps by that much in one tick; speeds don't. The held spot (`posRef`) is a position and was left behind, so the hold read an error of hundreds of metres. On a ship at 16 m/s this comes round about once a minute. It has been there since 0.1.0.
- **Evidence level.** The log has no position column, so the shift itself is not in it. The cause is inferred from the one-tick step, the game code, and the bench giving the same numbers (below).
- **Fix (`StovlOriginShiftFix`).** Any change of position in one tick that the jet's own velocity doesn't explain (8 m or more) is added to the held spot and the held height.

| Bench, 13.8 t, hover, position moved 1024 m / −960 m in one tick | 0.4.6 | 0.4.7 |
|---|---|---|
| Ground speed after it | 2.79 m/s | 0.00 |
| Bank | 8.9° | 0.0° |
| Off the spot 18 s later | 46.6 m | 0.0 m |

**22:55 log (the second ship take-off).** No such step in it. Two other things:
- A slow drift of 0.8 m/s over the deck during the second climb (16–19.7 s) with the hold asking 0.5 m/s. Not explained.
- At 23.67 s the STOVL law stops being run: its outputs freeze, the throttle goes back to the lever (0.20), thrust/weight falls 0.59 → 0.44 and the jet sinks and rolls over at 26.5 s. That is what STOVL freestyle (flight assist off) looks like in the log. The game log has no error. I can't tell from the log what switched it.

## 2. The side-stick pause (22:56 log)

Three places, two causes of the same kind.

| Log time | Input | What the jet did |
|---|---|---|
| 107.3–109.2 s | full right stick, lever at 45 kt | side speed 4.9 m/s for 7.3 commanded; bank target at 0.4° for 1.9 s while it accelerated 7 → 16 m/s forward |
| 33.4–33.8 s | full right stick during a fore-aft stop | bank target at 0.9° for 0.4 s |
| 63.5–65.2 s | stick released, 1.1 m/s drift left | bank ~0° while a 4.3 m/s sink was stopped at full thrust |

- **The governor.** A bank coming in tilts lift away, so the tilt governor (0.2.8) lets a bank increase only as fast as the thrust still in hand can make up the lift. With the thrust at its limit it allows nothing.
- **Cause 1: the forward budget.** A forward acceleration is made by the nozzle, and the lift fan's force stays vertical. The budget treated it as if the whole lift vector tilted (a circle), so it asked for more forward acceleration than the jet has. The thrust sat at about 97 % of what was available, past the 95 % line the FCS keeps for the height hold, and the governor was shut for as long as the jet accelerated. Bench, same jet: governor at zero for 4 s.
- **Cause 2: no credit for a tilt coming out.** At 33 s the pitch tilt was levelling after the stop (6.5 deg/s), which gives lift back. The governor counted spare thrust only, so the bank waited until the thrust had come down.
- **The third row is the vertical priority**, as designed (a sink being stopped takes the budget first). Not changed.

**Fixes.**
- `StovlFwdCapFromNozzle`: the forward part of the budget is what the nozzle can give with the lift still carried, from the nozzle's own force table, at the same 95 % thrust and thrust-lag reserve the sideways budget keeps. Forward and sideways demands are scaled by one factor so they divide what there is.
- `StovlGovTiltCredit`: lift a tilt gives back while coming out counts as budget for the other tilt coming in.

| Bench, Tom's jet (13.8 t) and settings | 0.4.6 | 0.4.7 |
|---|---|---|
| Stick to the side 3 s into a 0 → 45 kt acceleration: bank past 5° | 3.85 s after the stick | 0.87 s |
| — bank target frozen | 2.7 s | 0 s |
| — side speed 2.9 s after the stick | 0.2 m/s | 2.8 m/s |
| Stick to the side during a fore-aft stop: bank past 2° / 5° | 0.87 / 1.15 s | 0.35 / 0.82 s |
| — bank target frozen | 0.40 s | 0 s |
| — height during it | −0.05 / +0.10 m | −0.05 / +0.10 m |

**Cost: forward acceleration from the hover.** 0.4.6 took the 5 % thrust kept for the height hold; 0.4.7 doesn't.

| Hover → 30 kt | Acceleration 0.4.6 → 0.4.7 | Time to 27 kt |
|---|---|---|
| Buffed 11.9 t | 4.0 → 3.7 m/s² | 4.5 → 4.7 s |
| Buffed 20 % fuel | 4.4 → 4.4 | 4.3 → 4.3 |
| Buffed 13.6 t | 3.2 → 2.4 | 5.3 → 6.4 |
| Buffed + 500 kg on one wing | 3.7 → 2.9 | 4.7 → 5.5 |
| Stock 11.9 t | 2.7 → 2.2 | 5.9 → 7.0 |
| Stock 12.8 t | 1.8 → 1.25 | 8.3 → 11.2 |
| Tom's 13.8 t, hover → 45 kt: time to 39 kt | | 6.8 → 8.9 s |

Height during the acceleration: −0.10 / +0.12 → −0.04 / +0.08 m (Buffed 11.9 t). `StovlFwdCapFromNozzle = false` gives the 0.4.6 acceleration back, and the pause with it.

## 3. Pedal turn with forward speed, and the stick during it (22:56 log)

- **What the log shows.** 70–74 s: full left pedal at 22 m/s, stick released: 20.5 deg/s of yaw, bank to −29°, the skid growing to 13 m/s (37° of sideslip). 74–83 s: right stick, then left, then right, with the pedal held: the bank stays between −22° and −27° whatever the stick does; forward speed falls 17 → 9 m/s with 23 commanded.
- **Why.** Turning the nose at 20.5 deg/s at 22 m/s takes 8 m/s² sideways (39° of bank) to bring the velocity round with it. The jet had about 4.7. Nothing told the bank about the turn: the side-velocity loop only saw the skid once it was there. From then on the whole bank budget went into holding the skid (5 m/s² at 14 m/s forward), so a stick command of 7 m/s to the right still needed full left bank, because the jet was already doing 13.
- **Fixes.**
  - `StovlTurnCoordinate`: the acceleration demands carry the turn itself (forward speed × heading rate sideways, side speed × heading rate fore-aft), so the bank comes in with the yaw.
  - `StovlTurnYawLimit`: the pedal's yaw rate is no more than `StovlTurnShare` (0.707) × the sideways acceleration available ÷ ground speed; it builds and decays no faster than the bank can follow; and the yaw runs the bank loop's lag behind the bank's plan so the two arrive together. This is the rule that the FCS doesn't let the pilot command what the jet can't hold. In the hover and up to about 15 kt (13.8 t) nothing changes.

| Bench, 13.8 t, Tom's settings, full left pedal for 8 s, stick released | 30 kt: 0.4.6 → 0.4.7 | 45 kt: 0.4.6 → 0.4.7 |
|---|---|---|
| Largest skid | 8.1 → 0.8 m/s | 12.2 → 0.55 m/s |
| Largest sideslip | 30° → 2° | 36° → 1° |
| Forward speed during the turn | falls to 12.1 → holds 15.4 | falls to 14.3 → holds 23.1 |
| Yaw rate | 23 peak, sagging to 13 → 11.8 steady | 22 peak, sagging to 13–15 → 7.4 steady |
| Yaw rate 1 s / 2 s after the pedal | 18 / 22 → 5.5 / 11 deg/s | 17 / 19 → 4.5 / 7 deg/s |
| After the pedal is let go: skid | 4.1 m/s still to stop → 0.4 m/s inward | 12.2 → 0.3 |
| — bank past level the other way | 1.3° → 3.3° | 0.8° → 2.7° |
| — heading still to turn | 7.4° → 10.7° | 8.0° → 5.1° |
| Roll posts at their stop, total | 0.14 → 0.61 s | 0.27 → 0.75 s |
| Hover (pedal only, no speed) | 24.1 → 24.0 deg/s, the same | |

**Crossed controls** (30 kt, left pedal held, then full right stick): the bank comes out from −20° to −1° in 1.9 s, the jet lets the skid go to 5.4 m/s to the right (4.7 commanded) and holds the turn there; left stick takes it back to 3.4, right stick up again. In 0.4.6 the bank stayed at −15° to −21° with 8–10 m/s of skid whatever the stick did.

**Costs.**
- Yaw rate with speed: about 12 deg/s at 30 kt and 7.5 at 45 kt on full pedal (13.8 t), where 0.4.6 commanded 20 and the jet could not follow with its velocity.
- The yaw builds over about 2 s at speed (the bank's own pace) instead of 1 s.
- At 30 kt the nose turns 3° further after the pedal is released.
- More roll-post saturation with `StovlBankGainFromMargin` on.
- `StovlTurnShare = 1` gives 10.5 deg/s at 45 kt, with 1.5 m/s of skid held through the turn and 9° of bank the other way on release.

**Note on the stick at 35–50 kt forward.** There the stick blends from side-speed command to bank command (0.4.1's regime shift). At 22 m/s a right stick is partly a right bank, which with left pedal is a commanded skid. Unchanged.

## 4. Regression

Six jets (Buffed 11.9 t, 20 % fuel, 13.6 t, + 500 kg on one wing; Stock 11.9 t, 12.8 t), hover / mid-speed / vertical / short take-off / deceleration sets, changes off against on.

- **Identity.** With the five switches off the bench output equals the 0.4.6 build's on every column, all five sets (reference jet; difference 0.0).
- **Hover set.** Side stick, tap, fore-aft step and stop, gust, hover pedal: the same within 0.01–0.05 on every jet (side stop past zero 0.14 m/s, settled 4.10 s on the reference). No reversal counter changed.
- **Side + fore-aft together.** Stock 12.8 t reaches 7.0 kt in the 5 s instead of 13.3 (the forward share), height −0.06 → −0.03 m. Others within 0.5 kt.
- **Mid-speed pedal (half pedal, 30–38 kt).** Largest sideslip 10.4 → 8.8° (Buffed), 9.5 → 5.9° (Stock), 12.0 → 6.5° (13.6 t), 14.7 → 7.4° (one-sided).
- **Vertical and deceleration sets.** The same.
- **Short take-off.** Height at 40 s: 377 → 377 m (reference), 798 → 785 (20 % fuel), 229 → 221 (13.6 t), 355 → 331 (one-sided, with 8 km/h more speed); Stock within 2 m.
- **Landings, hover pedal at four weights, mission.** Summary lines the same within rounding, except the 15 t hard landing: same 5.23 m/s touchdown, main gear stroke 0.40 → 0.43 m of 0.50.

## 5. Not fixed, not checked

- **Flown: nothing.** The ship and the origin shift are compiled and bench-imitated only.
- **Forward acceleration** is lower (§2). The way to get more than 0.4.6 had is a nose-down tilt during the acceleration, as the stop already uses nose-up. Not built.
- **22:55 log:** the 0.8 m/s drift in the climb, and what switched the law off at 23.67 s.
- **One-sided jet:** roll posts at their stop 8.6 → 10.3 s in the mid-speed set.
- Second identity run (Stock 12.8 t) not completed.

## 6. Settings (Dev file, `9. STOVL`) and telemetry

| Key | Default | Off gives |
|---|---|---|
| `StovlOriginShiftFix` | true | 0.4.6 |
| `StovlFwdCapFromNozzle` | true | 0.4.6 forward acceleration, with the pause |
| `StovlGovTiltCredit` | true | 0.4.6 |
| `StovlTurnCoordinate` | true | 0.4.6 |
| `StovlTurnYawLimit` | true | the yaw axis's own limit at every speed |
| `StovlTurnShare` | 0.707 | (0.3–1) |

New log columns, at the end: `stUFwd`, `stVSide` (speed along the nose and to the right), `stPhiTrc` (bank the side-speed loop asks for), `stGovLam` (governor, 0 = shut), `stAH` (translation budget), `stFwdCap` (forward capability), `stPsiRate` (heading rate), `stOrigin` (origin shifts seen).

## 7. Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.4.7 and `meta.json`; 0.4.6 kept as `Vortex_FS20_FCS.dll.0.4.6.bak`.
- Dev DLL unchanged. No config change needed; Tom's own settings are kept.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.4.7-release`.

## 8. To check in flight

1. Ship take-off and a hover over the deck for two minutes. `stOrigin` in the log counts the shifts.
2. Lever to 45 kt from the hover, side stick during the acceleration: the bank should come in within a second.
3. Full pedal at 30 and 45 kt, stick released, then a crossed stick.
