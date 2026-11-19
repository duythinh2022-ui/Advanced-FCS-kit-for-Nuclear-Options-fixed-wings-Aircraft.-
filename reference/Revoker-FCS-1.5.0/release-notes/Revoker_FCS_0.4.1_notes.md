# Revoker FCS 0.4.1

Base: **0.4.0**. The same two corrections the Ifrit got, and they matter more here than expected. Patch bump: no new control law.

**Bench-only.** You flew 0.4.0 and said the roll feels plenty fast; this does not slow anything down.

## What your 0.4.0 log showed
Full lateral stick, by AoA band: the governor holds **33.5 deg/s at 15-25 deg** and 20.6 at 25-35, while the sideslip sits at 2.1 / 1.6 deg of an 8 deg margin. You cannot feel it because the rate is 138 / 102 deg/s there anyway - and measured on the bench, that hold costs **0.15 s of roll onset and no peak rate** (20 deg AoA entry: 138 deg/s and 1.45 s to 90 % with the window, 137 and 1.30 s with its gate held shut). It is the yaw-reach window pacing the entry, not the rate cap: at those conditions the model predicts 0.1-1.0 deg and the cap sits at its ceiling. Benign, left alone.

## 1. The yaw-reach window reads every effector (`PaceReachAll`, on)
The window holds the commanded roll to what the measured yaw can follow inside `PaceHorizon` - and it was computing its reach from the hand-picked yaw set (rudder + nozzle) that 0.4.0 removed from the *rate cap* but not from the *window*. That set leaves out the trailing edge, which is this jet's dominant yaw axis above ~45 deg AoA (aileron pair +-68 deg/s^2 at 350 km/h / 70 deg against the rudder's +-12). Now every allocator effector, each from where it is at its own slew inside its allocator window.

**This is most of 0.4.1.** The 68 deg / 300 km/h full reversal:

| | peak sideslip | peak velocity-vector roll |
|---|---|---|
| 0.4.0 | 2.5 deg | 44 deg/s |
| + jerk fix only | 3.3 | 42 |
| + window reach only | 1.7 | 80 |
| **0.4.1 (both)** | **1.2** | **80** |

Nearly double the reversal rate at half the sideslip.

## 2. The reversal model ramps at the trajectory's own jerk limit
It stepped straight to the acceleration *limit* (`RevFloorK * aRoll / cos a`) where the law, jerk-limited, realises far less. Worth little on its own here (see the table) but it is the correct form, and on the Ifrit the related unpaced-reversal error was the whole problem (`Ifrit_FCS_0.7.2_notes.md` section 1 - **not** ported here: on this jet the cap genuinely binds at high AoA and earns its keep, 82 vs 66 deg/s at 70 deg AoA with blend 0, so it stays as it is until flown).

## 3. New telemetry
`rateCap`, `revSimBeta`, `revSimT`, `paceReach` after `rollGate` - the 0.4.0 notes told you to watch `revSimBeta` and it was not actually in the log. It is now: it is the model's own predicted sideslip for a reversal at its cap, so a flight log says directly whether the model is honest.

## Bench, 0.2.22 -> 0.4.0 -> 0.4.1 (your config, margin 8)
| | stops W / mean | reversals W / mean | sweep roll | 65-70 deg cork pS | cork beta |
|---|---|---|---|---|---|
| 0.2.22 old stack | 4.2 / 2.57 | 7.9 / 3.91 | 93 | 72-81 | 1.7-3.3 |
| 0.4.0 (flown) | 3.7 / 2.26 | 4.9 / 2.60 | 99 | 62-71 | 1.1-2.5 |
| **0.4.1** | **3.7 / 2.24** | 5.2 / 2.71 | **100** | 62-70 | **1.1-2.4** |
Falling leaf 76-82 deg/s at 1.4-3.8 deg (0.4.0: 76-82 at 1.4-2.6 - the ias-150 case gives up 0.7 deg of sideslip for the reversal gain above). Roll entry at 20 deg AoA unchanged (138 deg/s, 1.42 s to 90 %, 1.54 deg).

## Install
- **Main:** `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 3241d13f169b85d14782dde6ac26cd0f), version 0.4.1.
- **Backup:** 0.4.0 kept as `…\Revoker_FS12_FCS.dll.0.4.0.bak`.
- A/B statics: `PaceReachAll` false = 0.4.0's window reach, `RevSim` false = the kinematic cap, `SlipFrameAlloc` false = the body-frame allocator.
