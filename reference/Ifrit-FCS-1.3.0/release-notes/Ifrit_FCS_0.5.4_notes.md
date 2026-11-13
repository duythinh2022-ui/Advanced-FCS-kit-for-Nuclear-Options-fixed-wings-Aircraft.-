# KR-67 Ifrit FCS 0.5.4

Base: **0.5.3**. Three changes to ground steering. Same changes as F-22E 2.4.14.

Compiled and bench-tested on the ground model. Not flown.

## 1. Pedal dead with the nose up on the mains (bug since 0.5.2)
The Ifrit runs the same code. The evidence is from the F-22E (**27 Oct 12:58 log, 381–386 s**). After the second touchdown the jet ran for 4.5 s on its mains with the nose up:
- 71 m/s, θ 3.8°, gear-down rate law. The ground rate law holds the attitude after a touchdown with the stick pulled.
- Full left and right pedal four times. The yaw rate stayed at **1.9 deg/s** the whole time. The rudders sat at ±5° toe and never moved with the pedal.

On the mains with the nose up, the gear-down law flies `rCmd = gsRCmd`. Since 0.5.2 (F-22E 2.4.12), in the stock phase `gsRCmd` is the *measured* yaw rate, so the rate error is always zero and the pedal does nothing.

**Fix.** That phase gets its own command, `gsRCmdMains`:
- **Pedal in:** the capped rate of 1.8.36–2.4.11, pedal × GroundLateralAccel / V (2 deg/s at 71 m/s).
- **Pedal released:** 0, ramped at 20 deg/s².
- **Line hold active:** the hold's command.

## 2. INDI nose wheel (release braking and line hold)
**While the pedal is in, steering is still stock.** Turn radius and reversals are unchanged.

After release, the nose wheel switches from the stock law (release braking) and the PI loop (line hold / touchdown hold) to INDI:
- **Virtual control:** the nose tyre's lateral force. Wanted force = force now + (wanted yaw acceleration − measured yaw acceleration) / yaw effectiveness. The force estimate and the measured yaw acceleration share one low-pass filter (`GroundIndiTau` 0.04 s).
- **Force now:** the game's tyre law, read from the IL: clamp(clamp(slip, ±10°) × response × (0.2 + 0.01 |wheelSpeed|), ±1) × μN, perpendicular to the contact velocity. The inputs come from the nose leg every tick: actual wheel angle, contact velocity (moving deck subtracted), leg load (compression + damping force), friction coefficient.
- **Yaw effectiveness:** the force's yaw moment about the FCS's CG divided by the FCS's yaw inertia, recomputed every tick. Fuel, stores and CG shifts are tracked automatically.
- **Everything not modelled shows up in the measured yaw acceleration:** mains, rudders, differential brake, gusts, model error.
- **Wheel command:** contact-velocity angle + the slip that gives the wanted force, capped at 1.2 × the tyre's saturation slip (more only skids). Below 1.5–3 m/s it blends to the pedal, as before.
- **Wanted yaw acceleration:** `GroundIndiGain` (15/s) × yaw-rate error. In the line hold, the command's slope is added on top. The release braking aims at zero rate; the hold aims at the line-hold rate.
- **Rudders and differential brake:** unchanged from 2.4.13.

## 3. Ground steering relative to the surface (moving carrier deck)
Reported: on a moving carrier the Ifrit can't turn its gear enough to get onto the ramps. Until now, everything the FCS steered by on the ground was in the world frame:
- **Straight-line hold:** on release it captured the *world* ground track as the line. On a deck that track is mostly the ship's own motion. Taxiing across the deck at 3 m/s on a ship doing 15 m/s, the track is 11° off the ship's heading while the nose points 90° across, so every pedal release yawed the jet back toward the ship's heading at the 4 deg/s hold limit.
- **Drift limit:** it faded in at 15–25 m/s of *world* speed, which a ship doing 15 m/s or more supplies on its own, and it acted on the *aerodynamic* sideslip. Wind over the deck at 90° to the nose reads as about 80° of "drift". With the limit at half strength and 8° allowed, that is 0.2 × 0.5 × 72 = full opposite yaw, whatever the pedal says.

The fix: the ground speed, the track, the line position and the drift-limit slip are now all taken relative to the surface under the wheels. It is the same velocity the game's tyres use: the contact collider's rigidbody point velocity plus an `AnimatedPhysicsSurface`'s velocity.
- **Turning deck:** the hold's line turns with the deck's yaw rate, and the yaw-rate commands include it.
- **Line position:** integrated from the surface-relative velocity. 0.5.2–0.5.3 used the global position.
- **Touchdown on a moving surface:** the hold captures the track over the deck instead of the world-frame approach track.
- **Fixed ground:** speed, track and line hold are unchanged.
- **Drift limit everywhere:** it now acts on the jet's slip over the ground, not the aerodynamic sideslip. On a runway in a crosswind, the wind no longer reads as drift; only an actual skid does. That is a behaviour change from 0.5.2–0.5.3, which steered against the crosswind's sideslip once past 8°.
- **Unchanged:** the stock law while the pedal is in still uses the game's own inputs (aircraft speed, yaw rate), the same as vanilla.

**Not tested:** none of this was tested on a carrier; the bench has no deck. The numbers above come from the old code's formulas.

## Bench results (`bench/groundsim/results_2414.txt` in the bench zip)
**Ground model fix.** gsim's tyre was corrected to the IL. The lateral force is now perpendicular to the contact velocity, scrub and brake drag act along −velocity, and the total is clamped to μN.

**Release braking**, after 3 s turns at full and half pedal, 4–25 m/s:
- At gain 15/s the INDI stops the yaw (|r| < 1 deg/s) within −0.13 to +0.03 s of 2.4.13. The best case is the F-22E at 7 m/s, below the stock FBW's 10 m/s minimum: 1.55 → 1.42 s.
- Swing-back is ≤ 0.21 deg/s.
- Release braking is limited by the nose tyre, so INDI can only match stock here, not beat it. At gain 5/s it was 0.2–0.3 s slower.

**Line hold**, with a 6 deg/s yaw kick plus 3 deg/s² rms yaw gusts, 10–70 m/s:

| Plant | Heading rms PI → INDI | Yaw-rate rms PI → INDI |
|---|---|---|
| Nominal | 0.08–0.19° → 0.02–0.03° | 0.14–0.32 → 0.04–0.05 deg/s |
| Yaw inertia +32 %, INDI not told | 0.11–0.26° → 0.03° | 0.18–0.41 → 0.06 deg/s |
| CG 4 % aft, INDI not told | 0.11–0.23° → 0.03° | 0.17–0.37 → 0.05–0.06 deg/s |

- **Peak after the kick:** the same at 10–25 m/s, where the tyre saturates; 13–22 % lower at 50–70 m/s.
- **In game:** the effectiveness is read live, so the mismatch rows are a worst case.

**Effectiveness cross-check (Ifrit taxi log fit).** About 5.4 deg/s² per degree of wheel at 12 m/s; the tyre law with the fitted load and inertia gives about 6.5.

## Seen in the logs, not changed
**27 Oct 13:08 Ifrit log.** Touchdown at 167.7 s:
- 4.0 g at θ 15.5°, banked −10°. The bank went to +7° and stayed there for the whole roll, at 55 m/s and 3° pitch. That suggests a damaged or collapsed main leg.
- The line hold was steering: wheel −1.9° to +1.1°, yaw-rate command −3.7 deg/s, taking out a 6.7 m drift.
- No pedal input after touchdown. The log ends at 173.5 s, at 52 m/s.
- **Nose stays up after a pulled touchdown.** With the stick neutral, the ground rate law holds θ; push to lower the nose. This is unchanged from earlier versions.

## Dev keys (Ground section)
- `GroundIndi` (`true`). `false` = 0.5.3: stock law on release, PI line hold.
- `GroundIndiGain` (15/s)
- `GroundIndiTau` (0.04 s)

## Telemetry
New columns:
- `gsWheel`: actual nose-wheel angle, deg
- `gsIndi`: INDI active this tick (1/0)
- `gsRdot`: filtered yaw acceleration, deg/s²
- `gsNu`: wanted yaw acceleration, deg/s²
- `gsFyK`: nose-tyre lateral force, kN
