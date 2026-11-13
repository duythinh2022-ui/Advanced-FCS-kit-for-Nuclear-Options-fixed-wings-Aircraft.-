# F-22E FCS 2.5.7

Base: **2.5.6**. Two of the governor's own devices retired, as asked (31 Oct): the margin cap and the yaw-reserve governor. The bench in this source is the **negative-AoA-calibrated** one (`claude/F22E_bench_negAoA_calibration_2.5.4.md`: camber-coupling fix, warm-started replays, `an/` scripts take `AN_OUT` / `BENCH_DIR` / `BENCH_DATA` from the environment).

## Change
- At level 3 with `RollPaceGovernor` on, the **margin cap** (`RollStopSideslip` / `50` / `65`, `RollStopSlipGain`) and the **yaw-reserve governor** (`RollStopYawHold`) no longer act: the pace governor is the whole roll-stop mechanism. Both still act below level 3 or with the pace governor off, and their telemetry (`stopSlipLim`, `stopSlipPred`, `stopSlipHold`) is still written. Static `PaceOwnsStop` (true) for A/B on the bench. Your 40/50/50 setting is now irrelevant at level 3 (it was already clamped to 15/20/20 and effectively off).
- The lateral law at level 3 is now: pace governor (open-loop yaw-authority pacing + the saturation-gated window) + the two AoA-rate caps + the symmetric roll trade + the measured-sideslip guard. Eleven of the thirteen limiters that were on at 2.5.0 are off or replaced; the guard and the yaw slew lead remain.

## Evidence
- **Flight (10:10 log, 2.5.4):** with the margin cap effectively off from ~155 s, sideslip at 5-36 deg AoA above 100 m/s was q95 2.9 / max 3.6 against 3.0 / 4.8 with 4/5/5 before it. The reserve governor's trigger (rudders past 80 %) is the window's gate (50-75 %).
- **Bench, recalibrated plant, stop / reversal sweep (32 cases, 450-850 km/h, 5-23 deg AoA):** pace alone worst stop / reversal 4.1 / 4.1, mean 2.39 / 2.83, roll 115 deg/s; with the two devices on at your 40/50/50: 4.1 / 4.1, 2.31 / 2.75, roll 112; with them on at 4/5/5: 3.1 / 3.9, 2.12 / 2.62, roll 106. So the 4/5/5 margin cap bought ~1 deg on the worst stop and 0.2 on the mean for 9 deg/s of roll; at your current setting the difference is 0.1 deg and 3 deg/s.
- **Regression scenarios:** identical with and without the two, except pull-then-push-over + roll 450 (3.7 -> 4.1) and the 1 g reversal (1.4 -> 1.5).
- **Held full-stick roll, 565 km/h, 24 deg AoA** (the reserve governor's own case): steady roll 91 vs 90 deg/s, rudders 49 vs 52 % mean, sideslip held 2.5 vs 1.8; the stop after it 3.1 vs 0.7 deg (the one place the reserve governor was visibly earning its keep - it had trimmed the reference before the release, the window had not). Within the old margin either way. The same case shows a 0.5 Hz, +-10 deg/s roll-limit oscillation from the 1.8.x TVC-margin roll governor (`rollGov`, nozzles cycling stop <-> 0) in every variant - pre-existing, and rare in the game (rollGov < 0.95 for 1-5 s per 300 s flight in today's logs; 2-7 dips).

## Not verified
- Not flown. The thing to watch is a long held roll at 20-30 deg AoA followed by a stop: the bench says 3 deg where the reserve governor gave under 1.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.5.7. Backup: 2.5.6 is `...\Aryx_F22E_StrikeRaptor_FCS.dll.2.5.6.bak`. Release: `C:\Games\NO mods\F22E-FCS-2.5.7-release` (source zip = 2.5.7 FCS + the calibrated bench + `tomcfg.cfg`). No new config keys. LogOutput: `F-22E FCS 2.5.7`.
