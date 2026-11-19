# F-22E FCS 2.4.17

Base: **2.4.16**. Three changes:
1. A roll-stop sideslip governor, `RollStopSideslip` 3 deg. It limits the roll rate while you are still rolling, based on the sideslip a stop or reversal would make.
2. A no-chase yaw command while the roll is being braked.
3. `PullSideslipMargin` default 9 -> 6, with a dev-file migration.

Bench only. Not flown.

## What the logs show
- **30 Oct 14:35 log, 267.7 / 271.3 / 273.9 s.** Rolls of ~80 deg/s at 24-26 deg AoA and 305 kt, flown with mode 3 (AoA/G law, `GAndPitchRate`).
  - The sideslip was -2.5 deg while rolling and went to **10.3, -11.1 and 12.6 deg** after each stop.
  - The 10:28 log shows the same thing at 61.9 / 139.5 / 277.3 s: 10.8, 10.8 and 13.0 deg at 20-30 deg AoA and 250-375 kt.
- **Why it happens:**
  - The rudders sit at their 35 deg stop through the whole roll, and the yaw rate still trails its command (r -27 against -36).
  - When the stick comes back, the wing stops the body roll in ~0.5 s. The yaw loop is still chasing the yaw it fell behind on, so the yaw rate grows for another 0.3 s (-31 -> -40 deg/s).
  - Only after that do the rudders slew across their 70 deg of travel (0.7 s at 100 deg/s) and brake at ~80 deg/s^2. The yaw rate that is left turns into sideslip at r cos(AoA).
  - So the answer to your question is both: the yaw can't catch up during the roll, and on the stop it overshoots its command.
- **Bench reproduction:** 565 km/h, the same 0.5 lateral stick for 0.7 s at 21-25 deg AoA gives **11.6 deg**, with the rudders going -35 -> +35. That is the same mechanism.
- **Why the airframe's yaw figure misled:** the FCS's `aYaw` (~180-200 deg/s^2) counts every surface's yaw range. What the rudders can actually brake with, from the effector tables, is ~85 deg/s^2 at this condition.

## Change 1: no-chase yaw command on a roll brake
- **When it acts:** while the roll trajectory is braking (a stop, a reversal, or a roll limit coming down). AoA 8-36 deg, fading out by 45. AoA/G and Rate laws only, never with AI.
- **What it does:** the yaw-rate command is not allowed above the yaw rate the jet already has on the same side. The yaw stops accelerating the moment the roll starts braking.
- **Feed-forward:** the yaw-rate feed-forward is computed from the coordinated command. The hold therefore never puts a step into it.
- **Bench, alone (no roll limit):** worst stop 12.5 -> 6.2 deg, at the same roll rate.

## Change 2: roll-stop sideslip governor (predictive roll limit)
- **Inputs:**
  - aB = the rudders' braking yaw acceleration, read each tick off the effector tables at the current flight state. It follows IAS, AoA, the rudder fade and a lost fin by itself.
  - r = pS sin(AoA), the yaw rate a velocity-vector roll pS carries.
- **The fit:** the peak sideslip of a reversal (the worst case) is about **2.1 x sin(AoA) x r^2 / aB**. Fitted on 116 bench rolls: 250-750 km/h, 4-36 deg AoA, 0.5 and full lateral stick, 1-8 g, stops and reversals. A stop with change 1 runs at about half of a reversal.
- **The limit:** the governor holds pS to `sqrt(RollStopSideslip x aB / (2.1 sin AoA)) / sin AoA`.
  - It never goes below 30 deg/s.
  - It eases down at up to 200 deg/s per s, so it never becomes a stop itself.
  - It fades out from 36 to 45 deg AoA.
- **Where it sits:** next to `PullRollTrade` in the lateral law. Rolling pulls get it as the AoA builds. It is on in the AoA/G and Rate laws, off in MPO, gear-down rate and AI.
- **What the cap looks like:**
  - 305 kt at 24 deg AoA: ~45 deg/s velocity-vector roll.
  - 350 kt at 9 deg: ~225 deg/s, so almost no cut.
  - 400 kt at 7 deg: no cut.
- **New dev keys** (3. Lateral-directional):
  - `RollStopSideslipGovernor` (default true; false = 2.4.16).
  - `RollStopSideslip` (default 3, allowed 1-15).

## Change 3: PullSideslipMargin 9 -> 6
- **New default:** 6.
- **Migration:** `AirframeRevision` 21 -> 22 moves a dev file still holding exactly 9 to 6, once. A value you set by hand is kept.
- **Your file:** it already holds 6, so it stays at 6 and only the revision stamp changes.

## Bench (your cfg, airframe from Aryx 1.0.4, 200 m, full throttle)
Sideslip peak after the stop or reversal. Roll = peak velocity-vector roll rate.

| km/h, AoA, lateral stick | Stop, 2.4.16 -> 2.4.17 | Reversal | Roll (deg/s) |
|---|---|---|---|
| 565, 24.7, full (≈ your 267.7 s roll) | 12.5 -> 1.1 | 17.2 -> 2.0 | 105 -> 47 |
| 565, 24.7, 0.5 | 8.6 -> 1.1 | 12.8 -> 2.1 | 89 -> 47 |
| 565, 18, full | 6.8 -> 2.1 | 12.4 -> 2.4 | 156 -> 70 |
| 450, 25.6, full | 9.3 -> 0.6 | 14.3 -> 1.7 | 86 -> 36 |
| 650, 19, full | 4.2 -> 2.2 | 6.9 -> 2.2 | 131 -> 70 |
| 650, 14.8, full | 5.6 -> 2.9 | 9.9 -> 3.8 | 209 -> 95 |
| 565, 13, full | 4.8 -> 3.1 | 7.6 -> 4.5 | 201 -> 112 |
| 650, 11.5, full | 4.9 -> 3.4 | 6.4 -> 5.1 | 237 -> 143 |
| 750, 7, full | 2.0 -> 2.0 | 3.4 -> 3.4 | 252 -> 252 |

**Whole sweep** (168 cases, AoA under 45):

| | Worst stop | Mean stop | Stops over 3 | Worst reversal | Mean reversal | Reversals over 3 |
|---|---|---|---|---|---|---|
| 2.4.16 | 12.5 | 3.22 | 32 | 17.2 | 4.89 | 45 |
| 2.4.17 | 8.2 | 1.85 | 10 | 7.8 | 2.75 | 28 |

**Still over 3 deg:**
- **Full-stick reversals at 350-400 kt and 9-13 deg AoA:** 4.5-5.1. That is sideslip from the roll's acceleration, and holding 3 there would roughly halve the high-speed roll rate.
- **The 34-41 deg AoA edge:** 5-8. The governor is fading out there and the high-AoA laws take over.

**Margin 5 instead of 3:**
- 565 / 24.7 / full: roll 60 deg/s, stop 1.4, reversal 2.9.
- 650 / 14.8 / full: roll 127 deg/s, reversal 4.8.

**Regression:**

| Case | Result |
|---|---|
| Pull, no roll (S2) | identical |
| 1 g roll, 350 km/h | identical (pMax 145 / 146) |
| 1 g roll, 250 km/h, 3 km | pMax 54 -> 28 deg/s, bank at 2 s 53 -> 42, sideslip 1.8 -> 0.9 |
| Held pull 0.4 + roll, 350 / 450 / 550 km/h | roll 50 / 102 / 83 -> 21 / 25 / 60 deg/s, sideslip 5.5 / 5.1 / 4.9 -> 0.9 / 1.5 / 2.5 |
| Full aft + full roll from wings level (2.4.16 scenario A), 250 / 320 km/h | AoA at 2 s 37 / 36 -> 58 / 54. The slower roll frees `PullRollTrade`, and the pull governor runs to its 6 deg margin (3.6 / 5.3) |
| Roll then pull, reversal at 600 km/h | 7.9 -> 3.9 |
| Push reversal, -14 deg AoA, 450 km/h | 4.6 -> 4.4 |
| Push reversal, 600 km/h | identical |
| AoA 45 and above | identical |

## Your split-flaperon idea
- **Method:** linear program on the effector tables, at 305 kt / 24 deg AoA, with the roll brake at 300 deg/s^2 and the pitch held at its trim. Maximum yaw available to brake the roll's yaw:

| Allocation | Yaw (deg/s^2) |
|---|---|
| Flaperons paired, stabs antisymmetric | 76 |
| + split flaperons (inner vs outer, decelerons) | 103 |
| + stabs used unevenly, TVC carrying the pitch (nozzles at their stop) | 149 |
| Everything independent | 177 |

- **The allocator already splits them:**
  - In the bench stop it delivered ~105 deg/s^2: rudders ~80, plus ~25 from the wing, with the right wing at outer +25 / inner -25.
  - The roll-fight cost has been off by default since 2.3.2, so the split is unconstrained.
  - Relaxing it further changed nothing: the sweep results were identical to 0.1 deg.
- **So it isn't worth a separate mode.** The remaining headroom is uneven stab use. That costs pitch and nozzle travel, so I did not try it.

## Not verified
- Not flown.
- The migration has not been run in game.
- **Bench lift differs from the game:** the bench jet makes more lift per degree of AoA. Your 24 deg / 5.6 g at 305 kt needs ~6.9 g on the bench. The limit reads AoA and the rudders' authority live, but the size of the in-game gain may differ.

## Telemetry
- **New CSV columns** (before the per-surface columns):
  - `stopSlipLim`: the roll limit, deg/s velocity-vector; 999 = off.
  - `stopSlipAB`: the rudders' braking yaw acceleration, deg/s^2.

## Install
- **Main:** `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.4.17.
- **Dev DLL:** unchanged (2.4.14).
- **Backup:** 2.4.16 is `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.16.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.17-release`.
- **Check:** `LogOutput.log` should show `F-22E FCS 2.4.17` and `2.4.17 defaults: PullSideslipMargin 6`.
