# F-22E FCS 2.6.0

Base: **2.5.16**. Level 3 only. The twelve retired lateral devices and their keys are removed from the FCS; the governor's knobs move to the player config. **No flight behaviour change** - the bench runs tick-identical to 2.5.16 at the flown settings.

## What is in the lateral law now
The reference governor - one rule, the yaw axis's capacity, read from the effector tables, the allocator's unmet-yaw report and the airframe's own sideslip tolerance (the probe):
- the roll-rate cap (reversal sized to the margin) blended with the yaw-reach window / acceleration cap (`RollPaceRateBlend`),
- the rolling-pull, sweep and shed pitch caps (`RollGovernorPitch`),
- the FF roll trade from the rudders' free yaw (`PullRollTrade`),
- the margin: one handling number (`RollStopSideslip`) capped by the probe (`RollStopSlipProbe`).
Plus the sideslip guard (the loop), the yaw slew lead, and the asymmetric-load limiters (damage case only). That is the whole sideslip path.

## Removed (archived verbatim in `C:\Games\NO mods\F22E-FCS-retired-governors\F22E_FCS_retired_governors_2.5.16.cs.txt`)
Code: 2.2.7 rolling-pull sideslip governor (+ PullFFBoost, cap release), 2.1.8 AoA-sweep yaw limit, 2.0.6 coordinated roll jerk, RollEntryCoord / BrakeYawCap / RevYawCap trajectory caps, 2.3.0 CoordinatedStop, 2.4.17/2.5.0 margin rate cap, 2.5.2 yaw-reserve governor, 2.4.17 no-chase; 75 of their constants. The 2.1.0-2.1.2 push + roll limiters stay for the asymmetric-load case only (clean jet: off, as at level 3). 501 lines out of the controller.
Keys (Dev, gone from the file and F1): `RollGovernorReplaces`, `RollPaceGovernor`, `RollPaceRateMode`, `RollStopSideslipGovernor`, `RollStopSlipGain`, `RollStopSideslip50`, `RollStopSideslip65`, `RollStopYawHold`, `RollStopNoChase`, `CoordinatedStop`, `AoASweepYawLimit`, `AoASweepYawLimitPositive`, `PullGovSmooth`, `PullFFBoostMax`, `PullSideslipMargin` (its /6 scale on the trade was 1.0 at the setting flown; dropped).

## Moved to the player config, section "6. Lateral governor"
`PullRollTrade` (0.7), `RollStopSideslip` (4), `RollGovernorPitch` (true), `RollPaceRateSlip` (1), `RollPaceRateBlend` (default now 0.4, the setting flown), `RollStopSlipProbe` (true). On first run the values in your Dev file are carried over once (your blend 0.4084507 included), then every old lateral key is dropped from the Dev file. LogOutput reports the carry-over. They show in F1 under the FCS entry for anyone, Dev DLL or not.

## Equivalence (bench, calibrated plant, blend 0.4084507 / margin 4)
Same 27-case reversal sweep and 18 regression scenarios run on the 2.5.16 and 2.6.0 benches: summaries identical (stop worst 6.9 / mean 3.04, reversal worst 7.8 / mean 4.04, mean roll 106 deg/s; every regression peak the same to the printed digit). Tick by tick, the first difference in every diverging case is 0.001 deg/s in `pSmax` / `pSTgt` - the float rounding of one removed no-op in the trade, `Mathf.Lerp(999, x, 1)`, which 2.5.16 did and 2.6.0 simply does not. Replicating that one rounding temporarily made all 45 cases tick-identical (max difference 0.000000 in beta / p / q / r / alpha); the chaotic full-aft cases then grow their usual bench sensitivity out of the 0.001 seed. Same law; the 2.6.0 numbers are the ones the Dev DLL's F1 shows at these settings. Not flown as 2.6.0 - it is 2.5.16's flown law.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.6.0; `BepInEx\plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 2.6.0. Backup `.dll.2.5.16.bak`. Release `C:\Games\NO mods\F22E-FCS-2.6.0-release`. LogOutput: `F-22E FCS 2.6.0`.
