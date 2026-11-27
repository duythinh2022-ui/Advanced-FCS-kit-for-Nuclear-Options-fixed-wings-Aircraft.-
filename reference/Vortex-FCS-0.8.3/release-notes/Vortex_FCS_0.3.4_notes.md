# FS-20 Vortex FCS 0.3.4 — loaded-roll sideslip: the allocator no longer trades missing roll for yaw

Build of 17 Nov 2026. **Benched, not flown.** On 0.3.3. Vortex only (Tom, 17 Nov): the change is in `FcsController.cs`, which
the other six jets also have; it is not ported to them.

## What was wrong (13:29 log, 92-98 s)
Loaded left roll at 565 km/h, AoA 10 on the load limit.
- **The transient made most of it:** in the roll entry (92.3-93.5 s) the roll rate reached -72 deg/s in ~0.7 s while the yaw lagged (+5 -> -7 deg/s, ~-12 needed). That built -6.5 of the -8 deg of sideslip.
- **Then nothing took it out (93.5-98 s):** the allocator weighs the lateral axes in the velocity frame, where sideslip acceleration = roll x sin AoA - yaw x cos AoA.
  - In that frame a roll it cannot deliver counts as yaw it no longer needs.
  - Roll was 150-230 deg/s^2 short (elevons at +-30) and yaw 30-45 short. At 10 deg AoA, 150 x sin 10 ~ 26 against 30 x cos 10 ~ 30: the sideslip-axis error was about zero.
  - So the canted rudders stayed off yaw: differential 0-5 deg of 30. Your pedal added yaw demand and the rudders moved at once.
- The squared-cost explanation in the 0.3.3 notes was wrong. Option (a) as written there (hold each axis to its reach) was tried and dropped: it did not change the solution on the bench.

## What changed
- **`SlipCreditFade`** (Dev, "4. Gains", on): as the roll shortfall grows from `SlipCreditRoll0` 40 to 100 deg/s^2 (last tick's unmet roll), the allocator's frame turns from the velocity frame toward the body frame.
  - The angle used for the frame goes from the AoA toward 0.
  - It moves at full scale in `SlipCreditTau` 0.4 s.
  - Only below `SlipCreditAoA0` 20 deg AoA, fading out by 30. At high AoA the velocity frame is the right one (Revoker 0.4.0).
- Telemetry: `slipCredK` (0 = velocity frame, 1 = body frame) after `gsLn`.
- `SlipCreditFade` false = 0.3.3.

## Bench (0.3.3 = this build with SlipCreditFade false)

**Heavy load** (1760 kg on wing2a_R, full aft stick, full left roll; 470 km/h at throttle 0.6, the rest AB or half throttle): bank in 5 s / max sideslip

| Case | 0.3.3 | 0.3.4 |
|---|---|---|
| 470 km/h | 316 deg / 4.0 | 320 / 1.8 |
| 330 km/h | 239 / 8.1 | 336 / 2.4 |
| 300 km/h | 224 / 9.0 | 325 / 3.8 |
| 400 km/h | 298 / 6.3 | 336 / 2.3 |
| 400 km/h, half throttle | 264 / 5.9 | 283 / 2.7 |
| 550 km/h, half throttle | 376 / 2.7 | 355 / 2.0 |
| 330 km/h, half aft stick | 264 / 8.1 | 314 / 1.8 |
| 250 km/h, 1000 kg | 269 / 6.3 | 270 / 3.0 |
| Right roll (toward the load) | 416 / 2.4 | identical |

**Sideslip put in with 1.5 s of pedal during the loaded roll, then released (565 km/h, load at 10 deg AoA):**
- 0.3.3: -12 deg, still -5.9 after 2.5 s and -2.8 after 5 s; roll -50 to -65 deg/s.
- 0.3.4: -12 -> 0 in ~1.9 s; roll back to -72 to -76 deg/s.

**Roll entry, same case without the pedal:** max sideslip 3.0 -> 1.8 deg. Bank in 5 s 378 -> 365 deg (the roll gives a little to the yaw at entry).

**650 kg set** (wingtip_L)
- 250 km/h: sideslip 4.4 -> 1.4 deg, roll +36 -> +52 / -33 -> -27 deg/s.
- 400 km/h: 5.6 -> 3.5 deg, max AoA 13.6 -> 15.8 (the load limiter sees more roll in hand).
- 600 km/h: 3.4 -> 3.8 deg.
- Store drops in a pull: identical.

**Regression suite**
- Stop / reversal sweep: worst sideslip 4.6 / 5.8 -> 4.5 / 5.7 deg, mean 3.21 / 4.31 -> 3.00 / 3.98.
- Better: held 0.4 + roll 3.6 -> 3.0; pull-then-roll 320 3.7 -> 3.3; roll-then-push -55 2.9 -> 2.7.
- Worse:
  - pull-then-roll 250: 2.4 -> 2.9
  - pull-then-pushover + roll 350 / 450: 3.9 / 4.1 -> 4.3 / 4.8
  - full aft + roll + reversal 500: 3.6 -> 3.8
  - roll-then-pull reversal 400: 3.1 -> 3.4
- Corkscrews: peak sideslip within -0.5 / +0.6 deg (worst 3.8 -> 4.4 at 250, 3.4 -> 4.0 at 350, pull 0.8); AoA swing +1 to +4 deg in four cases.
- Damage suite: identical to 0.1 deg.

**Other sets**
- Velocity-vector rolls at 500 km/h: sideslip 5.7 / 5.2 / 1.9 -> 5.5 / 5.0 / 1.9.
- Falling leaf and reversal: identical.
- STOVL (hover, VL, VTO, decel, transition): identical.

**Curves:** in the loaded rolls the roll-rate ripple goes 0.15-0.30 -> 0.21-0.51 deg/s rms (residual about a 0.25 s average; the rolls are 50-75 deg/s), and rudder steps of more than 5 deg per tick go 38 / 58 -> 22 / 38 in the two worst cases. With a 0.15 s fade the 500 km/h velocity-vector roll lost 1.3 deg of sideslip margin; 0.4 s does not.

## Not done
- The departure (169-184 s) is not addressed; see the 0.3.3 notes. The yaw weight still drops to 2 above 20 deg AoA with sideslip (`YawPrioHi`).
- The roll-entry yaw lag itself is unchanged; the fade acts once the roll is saturated.

## Install
- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.3.4; 0.3.3 kept as `Vortex_FS20_FCS.dll.0.3.3.bak`. `meta.json` updated.
- New Dev keys, no migration.
