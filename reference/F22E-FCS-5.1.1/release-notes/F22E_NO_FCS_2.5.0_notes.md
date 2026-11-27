# F-22E FCS 2.5.0

Base: **2.4.18**. The roll-stop sideslip governor is rebuilt as a reference governor on the commanded roll rate, with its prediction fitted on your 30 Oct flight logs instead of the bench. 2.4.19 (rudder-paced roll brake, never installed) is dropped.

Fitted on flight data, checked on the bench, **not flown**.

## What the logs say (7 logs of 30 Oct, 320 stops and reversals, 200 used: 3-40 deg AoA, over 25 deg/s)
- **Peak sideslip after a stop or reversal ≈ 2.0 + |pS| sin(AoA) cos(AoA) × τ**, with
  - **τ ≈ 0.054 s with the rudders off their stop.** Roll rate adds little: median 2.3 deg at 80 deg/s and 15 deg AoA, 3.7 at 155 deg/s, 4.7 at 190. Stops from rolls that had settled with rudder margin were benign even fast: 14:35 log 85.0 s (152 deg/s, 20 deg AoA) 1.1 deg, 152.9 s (124 deg/s, 29 deg AoA) 1.1 deg.
  - **τ ≈ 0.27 s with the rudders pinned at the release, above ~125 m/s IAS.** Every 10-16 deg spike was this: 14:35 log 267.6 / 271.2 / 273.8 s (57-75 deg/s at 24-26 deg, half stick, 0.45-0.6 s into the roll), 10:28 log 61.8 / 139.3 / 277.1 s, 14:31 log 182.2 s (a 3.7 s roll where the pull at the end loaded the rudders to their stop).
  - **Pinned at 84-117 m/s IAS: benign** (9 stops, 1.2-5.5 deg; 10:28 log 126.3 / 231.3 s at 2.6 / 1.9). So the pinned term is gated off below 110 m/s and in from 130.
  - Neither term depended on IAS or on the rudders' braking authority otherwise. That is why 2.4.17 (quadratic in yaw rate over rudder authority, fitted on the bench) held low-IAS rolls to its 30 deg/s floor while they stop cleanly in game.
- Fit quality: rms 1.9 deg, correlation 0.65; 60 % of the logged stops at or under the prediction, 67 % at the default gain 1.3. The rest is scatter the model does not carry: pulls during the stop (AoA rising 13-18 deg/s), the 22:36 log 50.9 / 56.1 s 8-9 g reversals (yaw overshoot), and the odd event.
- **Steady-roll rudder load ≈ 0.01 × the yaw rate carried (pS sin AoA).** The rudders only sit at their stop in a steady roll past ~70 deg/s of yaw rate; the pinning before the spikes was the roll entry (the yaw lags the roll by 0.3-0.5 s) or a pull.

## Changes
1. **`RollStopSideslipGovernor` rebuilt (dev, default true).** Caps the commanded velocity-vector roll rate to the rate whose predicted stop stays within the margin. Margin schedule: `RollStopSideslip` **4** (3 → 4, migrated) up to 10 deg AoA, `RollStopSideslip50` **6** at 50, `RollStopSideslip65` **8** at 65, linear between, off past 70. `RollStopSlipGain` **1.3** scales the prediction (1 = median fit).
   - Only the commanded rate is capped. The trajectory's acceleration and braking are untouched.
   - **Reference-governor dynamics:** the cap comes down at 0.35 × (rudder braking yaw / sin AoA), 20-100 deg/s per s, and rises at 1.5× that. Never while the trajectory is still accelerating into the roll. 2.4.18's gate, engaging over 0.3 s mid-roll, braked a 9 g roll from 80 to 25 deg/s with the rudders pinned, then let go and did it again at ~1 Hz (23:27 log, 95.6-97.3 s: sideslip -5.3 → +1.3 → +6.5). The rudder-load input is averaged over 0.7 s.
   - **Cap by AoA, gain 1.3** (unloaded rudders / pinned above 130 m/s; `+1` = with `RollStopSideslip` 5):

     | AoA | margin | unloaded | pinned | unloaded, +1 deg |
     |---|---|---|---|---|
     | 5 | 4.0 | none | 65 | none |
     | 10 | 4.0 | 167 | 33 | 250 |
     | 15 | 4.2 | 128 | 30 | 185 |
     | 24 | 4.7 | 104 | 30 | 142 |
     | 30 | 5.0 | 99 | 30 | 132 |
     | 36 | 5.3 | 99 | 30 | 129 |
     | 45 | 5.8 | 107 | 30 | 135 |
     | 65 | 8.0 | 223 | 44 | 260 |

   - **Honest consequence of 4 deg at low AoA:** your 250 deg/s rolls at 10-15 deg AoA are capped to ~130-170, because the logs put their stops at 3.7-4.8 deg (median / p75). If that is not what you want, `RollStopSideslip` 5 frees them (last column) and still cuts the moderate-AoA rolls to ~130-140.
2. **`RollStopYawHold` (new, dev, default true).** While the roll is still building and the rudders are at their stop for its yaw (above the same 110-130 m/s gate, fading out 36-45 deg AoA), the roll-rate command may not run further ahead of the roll rate the jet already has. It never brakes (never below the rate in progress); it stops the roll outrunning the yaw and lets go as the rudders come off their stop. This is for the short rolls the slow cap cannot reach in time: every 12-16 deg stop came 0.45-0.85 s into the roll with the rudders pinned, and from 57 deg/s as much as from 117. Cost: a pause in the entry at moderate AoA (bench, 305 kt / 22 deg, full stick: the roll takes ~0.5 s from 60 to 90 deg/s instead of ~0.15). **This is the one part that reads as a slower entry, so it has its own switch.**
3. **Removed:** the 2.4.17 cap formula and the 2.4.18 rudder-load gate (`RollStopRudderLoad` is unused; delete the line from the dev file or ignore it). No-chase (`RollStopNoChase`) and the yaw slew lead (`YawSlewLead`) stay as in 2.4.18.
4. **Migration:** `AirframeRevision` 22 → 23 moves a dev file still holding `RollStopSideslip` 3 to 4 once; a value set by hand is kept. LogOutput: `2.5.0 defaults: RollStopSideslip 4`.
5. **Telemetry columns** (before the per-surface columns): `stopSlipLim` (the governor's own cap, deg/s; compare with `pSmax`), `stopSlipAB` (rudder braking yaw, deg/s²), `stopSlipU` (rudder load for the roll's yaw, raw), `stopSlipPred` (predicted peak sideslip of a stop from the roll in progress), `stopSlipMargin` (margin in force), `stopSlipHold` (yaw-saturation hold, 0-1), `yawLead`.

## Bench (your cfg, airframe from Aryx 1.0.4, 200 m, full throttle)
The bench disagrees with the game on exactly this quantity: its rudders sit at their stop through a steady 90 deg/s roll at 24 deg AoA where the game's sit at ~40 %, and its sideslip after stops at 170-215 kt runs 5-7 deg against 1-3.5 in game. So the bench below checks that the governor behaves (no cycling, no self-made brake, regressions) and gives relative numbers only; the prediction itself comes from the logs.

**Dynamics:** held full-stick roll, 565 km/h, 21-31 deg AoA: cap 105 → 95 → 85 as the AoA rises, no cycling (the first cut, 400 deg/s² rise against 100 fall, cycled 57-72 at 3 Hz and was changed). 450 km/h, 35-60 deg AoA: cap 52-43, binding the whole way.

**Stop / reversal sweep, 144 cases, 3-45 deg AoA** (peak sideslip after the stop, mean velocity-vector roll rate; bench):

| | Stop worst / mean | Reversal worst / mean | Mean roll | Bank at 1 s |
|---|---|---|---|---|
| 2.4.16 | 12.5 / 4.1 | 17.2 / 6.4 | 123 | 107 |
| 2.4.18 | 8.3 / 2.9 | 7.8 / 4.4 | 113 | 103 |
| 2.5.0 | 8.3 / 3.4 | 11.2 / 4.8 | 103 | 99 |
| 2.5.0, hold off | 8.3 / 3.2 | 11.7 / 4.8 | 105 | 100 |

By AoA band (mean sideslip / mean roll rate): 3-10 deg: 2.2/162 → 2.0/143; 10-17: 4.6/166 → 3.6/126; 17-24: 6.7/116 → 4.4/105; 24-31: 12.1/90 → 8.3/78 (2.4.18: 2.8/71); 31-36: 5.0/59 → 3.6/57. The 24-31 band is where 2.4.18's deep cut did better on the bench; its rudders are pinned there and the game's are not, so this is the band the flight test decides.

**Replays of logged stick inputs** (bench): 23:23 log 116 s 9 g reversal 12.3 (2.4.16) / 9.3 (2.4.18) / **6.7**; 23:27 log 382 s 0.45-pull reversal 13.5 / 11.6 / **5.2** (the hold); 23:27 log 316 s not reproducible (bench reaches 64 deg AoA where the game was at 31).

**Regression** (2.5.0 against 2.4.16): pull without roll, 1 g rolls at 250/350 km/h, full aft + full roll from level (250-600 km/h), roll then pull, push reversals, AoA 45 and above: identical or within 0.1 deg / 1 deg/s. Pull-then-roll: identical except 600 km/h (121 → 106 deg/s). Held 0.4 pull + full roll: 134/155/250 → 102/115/181 deg/s at 350/450/550 km/h, sideslip 4.3/4.9/4.3 → 3.8/3.1/3.4.

## Not verified
- Not flown. The fit is on your logs, the dynamics on the bench.
- The hold (`RollStopYawHold`) could not be judged on the bench because the bench's rudders never come off their stop in a steady roll; in game they do, which is what releases it.
- Whether 4 deg at low AoA is the margin you want (see the table).

## Install
- **Main:** `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.5.0.
- **Dev DLL:** unchanged (2.4.14).
- **Backup:** 2.4.18 is `...\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.18.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.5.0-release`.
- **Check:** `LogOutput.log` shows `F-22E FCS 2.5.0` and `2.5.0 defaults: RollStopSideslip 4`.
- **To A/B in one session:** `RollStopYawHold` false (hold off), `RollStopSideslipGovernor` false (cap off; no-chase and yaw lead stay = 2.4.18 minus its gate), `RollStopSideslip` 5 (frees low-AoA rolls).
