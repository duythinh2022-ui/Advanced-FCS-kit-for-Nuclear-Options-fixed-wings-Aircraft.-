# Vortex FCS 0.5.24: side and fore-aft commands no longer wait for a sink stop

20 Nov 2026. On 0.5.23.

**Status: benched, not flown.**

Tom, 20 Nov:

> when i command negative v/s, and then i release the stick, while the fcs is stopping the sink rate, i command side skid, but the fcs wont take my side skid command until it has stopped the sink rate. Fix that, and see if it happens to yaw and forth/back velocity command

## What the log shows (`Vortex_FCS_20261120_105047.csv`, 12.1 t)

**Released from a −5 m/s descent at 684.4 s and 725.3 s:**
- The stop asked for the full spare lift: about 3.9 m/s² upward for ~1.6 s.
- The translation budget (the thrust left over for tilting) was **0** for the whole stop, and the tilt governor's headroom was 0.
- The side stick sat at full with 0.6–0.8° of bank until the sink was gone. The bank only started at 686.1 s and 726.8 s.

**Why:** 0.5.14 (`StovlClimbShare`) made a stick **climb** share the thrust with the translation. Stopping a descent kept its whole demand, so it took everything first.

**Yaw: not affected.**
- 428.2 s: pedal during a stop with the translation budget at 0. The yaw rate built at ~21 deg/s², the same as with the stop finished (361.1 s).
- The yaw nozzle does not draw on the tilt budget.

**Fore-aft lever: affected the same way as the side stick.** It uses the same budget. In this log the lever was never moved during a stop, but the bench shows it.

## Change (`StovlSinkStopShare`, dev, on)
- **Shared:** stopping a descent is now one more demand on the shared budget, like a climb. Two cases:
  - the released stick's stopping profile;
  - the stick asking for less sink than the jet has.

  The stop and the translation each get a share when both ask for more than the thrust holds, and each may use what the other leaves (the same `ClimbPart` split as the climb).
- **Kept whole: what the ground needs:**
  - the deceleration that brings the descent down to the touchdown rate in the height left above the touchdown envelope;
  - the envelope's own deceleration and the height integral;
  - everything, once the sink is at or past what the envelope allows at this height.
- **Not shared:**
  - the height hold's own small dips (a translation's sink);
  - flying a height deficit back.

  Both keep their whole demand, as 0.5.14 has it, so hover height holding is untouched.
- **Cost:** translating during a stop makes the stop a little slower and loses a little more height. The height hold flies it back once the descent has stopped.

## Bench: 12.15 t, ~72 m, −5 m/s descent released, input 0.1 s later

| Input during the stop | | 0.5.23 | 0.5.24 |
|---|---|---|---|
| Full side stick | Bank 1.8 s after input | 0.5° | **13.7°** |
| | Side speed at 1.8 s | 0.14 m/s | **2.05 m/s** |
| | Height lost | 5.42 m | 6.30 m (+0.9) |
| | Sink stopped after | 2.03 s | 2.43 s |
| Lever to 60 % | Forward speed at 1.8 s | 0.40 m/s | **5.9 m/s** |
| | Height lost | 5.40 m | 6.66 m (+1.3) |
| | Sink stopped after | 1.97 s | 2.68 s |
| Full pedal | | unchanged | unchanged (yaw rate 26 deg/s at 1.7 s either way) |

**Response curves:**
- **Bank rate** rises smoothly from 0 to 9.6 deg/s, with no hold at level.
- **Sink stop** decelerates at a steady 2.1–2.4 m/s². There is one small dip, 2.4 → 1.4 → 2.4 m/s² over 0.4 s, where the bank peaks and the translation's share is at its largest.
- **Fore-aft:** pitch attitude held within 0.2°.

**Other sets:**
- **12-case set:** identical to 0.5.23. An earlier draft that also shared the height hold's own dips changed 8 of the 12 cases, so it was narrowed to descent stops.
- **Regression set** (vertical landings, hovers, vertical and short take-off, mission): unchanged.

## Not checked
- Not flown.
- Translating during a sink stop **near the ground** is protected by the envelope rule above but not benched as its own case. The landings in the regression set are unchanged.

## Release

Main DLL 0.5.24. Release folder `C:\Games\NO mods\Vortex-FCS-0.5.24-release`. New dev switch `StovlSinkStopShare` (on); false = 0.5.23.
