# F-22E FCS 2.4.18

Base: **2.4.17**. Three changes:
1. The roll-stop governor now acts only while the rudders are actually loaded.
2. A new yaw slew lead.
3. No-chase gets its own switch and a fix for rolling pulls.

Bench and log-replay only. Not flown.

## Why
- **22:36 log, 269.9-285 s.** These are 155-170 kt unloading rolls: full lateral stick while the AoA falls from 45 to 3 deg.
  - Through 34-17 deg AoA the roll was held at the governor's 30 deg/s floor.
  - Every other limit allowed 70-119 deg/s, which is what 2.4.16 would have given.
  - The rudders were at 1-9 deg and the sideslip stayed under 1 deg.
  - The 2.4.17 prediction (rudder braking authority only, about 20 deg/s^2 at that IAS) misses how much of the yaw the airframe's own directional stability carries at low IAS.
- **Stops that stayed benign in game.** In the 10:28 log, stops at 170-215 kt and 22-37 deg AoA ended at 1-3.5 deg (126.4, 190.0, 197.8, 231.4, 263.7, 284.0 s). The bench model says 5-7.
- **Stops that spiked (10-13 deg).** Every one had the rudders at or near their stop before the release: 14:35 at 267.7 / 271.3 / 273.9 s, and 10:28 at 61.9 / 139.5 / 277.3 s.
- **22:36 log, 50-53 s.** Here the 2.4.17 limit held the roll to 30-51 deg/s and the sideslip still reached -12.4 deg.
  - The yaw ran past its own command: r 42 against rCmd 28 -> 7.
  - The rudders were still swinging back from their +35 stop when the reversal came.
  - This is the yaw loop ignoring the rudders' slew time, not too much roll.

## Changes
1. **Rudder-load gate on `RollStopSideslipGovernor`.**
   - **The load:** the rudders' yaw moment now, toward the side the roll needs, over what they have on that side (from the effector tables), averaged over 0.2 s.
   - **The gate:** the roll limit fades in from `RollStopRudderLoad` (dev, default 0.6) to 0.3 above it, over 0.3 s, and lets go over 0.8 s.
   - **Replayed on the logged rudder positions:**
     - The 22:36 slow rolls peak at 0.17, so the limit stays at 70-119 instead of 30.
     - All six spiking stops are fully engaged 0.2-0.6 s before the release.
     - The 10:28 126.4 / 231.4 s stops (rudders at their stop, sideslip 2.6 / 1.9) also engage.
   - **`RollStopRudderLoad = 0`** gives 2.4.17's behaviour.
2. **`YawSlewLead` (new, dev, default true).**
   - **What it does:** the yaw-rate loop looks ahead along the measured yaw acceleration by the rudders' time to swing back through neutral (their antisymmetric deflection / servo rate). It is the same idea as the 2.2.5 pitch lead for the nozzles.
   - **When it acts:** only where the yaw is predicted to reach or pass its command within that time, judged against the command's own rate. Roll entries keep their yaw build-up. With the rudders near neutral it does nothing.
   - **Where:** AoA/G and Rate laws, fading out from 36 to 45 deg AoA.
3. **`RollStopNoChase` (new, dev, default true).**
   - **Own switch:** no-chase is separate now. In 2.4.17 it went with `RollStopSideslipGovernor`, so turning the governor off turned it off too.
   - **Rolling-pull fix:** it now acts only while the yaw the roll needs, |pS| sin AoA, is shrinking, or while the AoA isn't rising faster than 8 deg/s. In a roll-then-pull, the trajectory brakes the roll while the rising AoA raises the yaw it needs. The 2.4.17 hold built sideslip there (bench, 400 km/h: 2.9 -> 4.9 deg); now 2.9.
- **Telemetry, new columns:** `stopSlipU` (rudder load), `stopSlipW` (gate) and `yawLead` (s).

## Bench (your cfg, airframe from Aryx 1.0.4, 200 m, full throttle)
Peak sideslip after the stop or reversal; peak velocity-vector roll rate.

| km/h, AoA, lateral stick | 2.4.16 | 2.4.17 | 2.4.18 |
|---|---|---|---|
| 565, 24.7, full, stop / reversal | 12.5 / 17.2 at 105 deg/s | 1.1 / 2.0 at 47 | 2.5 / 3.9 at 83 |
| 565, 24.7, 0.5, stop / reversal | 8.6 / 12.8 at 89 | 1.1 / 2.1 at 47 | 2.4 / 3.8 at 81 |
| 565, 21, 0.5 for 0.7 s, stop (≈ your 267.7 s roll) | 11.6 at 79 | 1.5 at 43 | 4.3 at 74 |
| 450, 25.6, full | 9.3 / 14.3 at 86 | 0.6 / 1.7 at 36 | 1.5 / 3.4 at 60 |
| 565, 18, full | 6.8 / 12.4 at 156 | 2.1 / 2.4 at 70 | 3.6 / 6.0 at 117 |
| 650, 14.8, full | 5.6 / 9.9 at 209 | 2.9 / 3.8 at 95 | 4.3 / 7.2 at 161 |
| 750, 7, full | 2.0 / 3.4 at 252 | same | same |

**Within 3-36 deg AoA (168 cases):**

| | Worst stop | Mean stop | Worst reversal | Mean reversal | Mean roll |
|---|---|---|---|---|---|
| 2.4.16 | 12.5 | 3.43 | 17.2 | 5.38 | 135 deg/s |
| 2.4.17 | 3.5 | 1.78 | 7.6 | 2.81 | 112 deg/s |
| 2.4.18 | 4.4 | 2.21 | 7.8 | 3.71 | 127 deg/s |

**It does not hold 3 deg everywhere.**
- **Fast roll entries:** the rudders saturate in ~0.35 s, before the gate (0.2 s average + 0.3 s ramp) has engaged. That is your 267.7 s type: 4.3.
- **High-speed full-stick reversals:** 6-7.
- **`RollStopRudderLoad` is not a monotonic trade** (game-like stop):

| `RollStopRudderLoad` | Sideslip | Roll |
|---|---|---|
| 0 | 1.6 | 47 deg/s |
| 0.4 | 5.1 | 68 |
| 0.6 | 4.3 | 74 |
| 0.8 | 6.3 | 79 |

  Engaging partway through a roll whose rudders are already at their stop is itself a stop, so the middle values don't simply trade sideslip for roll rate.

**Regression:**

| Case | Result |
|---|---|
| Pull, no roll | identical |
| 1 g roll, 350 km/h | identical (145) |
| 1 g roll, 250 km/h | pMax 54 -> 37 (2.4.17: 28) |
| Full aft + full roll, 250 / 320 / 400 / 500 / 600 km/h | sideslip 6.3 / 4.7 / 3.8 / 3.8 / 4.1 -> 4.4 / 3.9 / 3.4 / 3.8 / 4.1 |
| Roll then pull | same as 2.4.16 within 0.4 deg |
| Push reversal | unchanged (4.4 / 2.3) |
| AoA 45 and above | unchanged |

## Not verified
- Not flown.
- The gate was replayed from logged rudder deflections, not from the moment-based load the DLL uses.
- The bench is pessimistic at low IAS compared with the game.
- The yaw-lead effect on the 22:36 50-53 s case is not reproduced on the bench, which doesn't fly that rapid 8 g stick sequence.

## Install
- **Main:** `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.4.18.
- **Dev DLL:** unchanged (2.4.14).
- **Backup:** 2.4.17 is `...\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.17.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.18-release`.
- **No config migration.** The new keys appear in the dev cfg with their defaults on first launch.
