# F-22E FCS 2.5.12

Base: **2.5.11**. One correction inside the pace governor, from the 13:31 log (2.5.11, `RollPaceRateBlend` 0.5, margin 15): the yaw the cap credits is derated by the allocator's own unmet-yaw report. Not on top - the cap's input, not its output. Flown-only (the bench's yaw at 65 deg AoA does not saturate like the game's).

## What the 13:31 log showed
Sideslip max 11.1, q95 5.8, 2.6 s above 5 deg in 42 s airborne. Tom: "50 % roll rate is good enough" (the blend works; mean roll in the 15-30 deg band as 2.5.8 + ~20 %). The 11.1 at 58.3 s: full lateral stick at 65 deg AoA / 134-150 m/s, the cap at 64-68 deg/s of velocity-vector roll (body yaw 60); stick released at 55.8 s with that rotation on; the yaw axis shed ~25 deg/s^2 against a table authority of 100-190 (`yawSat` 1.0 for 2 s - past the rudder fade the stabs are the yaw axis and have the pitch to do); the AoA then fell 42 -> 27 under gravity with the stick centered (q ~ -1, not a pitch command) and the 30-40 deg/s of yaw still on board became sideslip. After the release nothing in the law can act (target already 0, AoA sweep not commanded); before it, the cap was too generous because its yaw figure was wrong.

## Change
- Rate cap and open-loop acceleration cap: the credited yaw x (1 - derate x `yawSat`), derate = `PaceSatDerateHi` 0.5 past the rudder fade, `PaceSatDerateLo` 0.25 below it (lerped with `rudderAuth`). `yawSat` is the allocator's unmet yaw, 0..1 over 10-50 deg/s^2, 0.25 s filter. During the 55 s roll it read 0.2-0.9: the 64 deg/s cap would have sat at ~40-55. Statics for the bench.
- Dev-unlock DLL version string 2.5.12. No new config keys.

## Bench
Reversal sweep (margin 15): blend 0.5 worst / mean 9.3 / 6.1, roll 71 (2.5.11: 9.5 / 6.4, 71); blend 1: 6.9 / 4.2, roll 52, reversal 0.32 s (2.5.11: 5.9 / 4.0, 52, 0.39) - within scatter. Regression: rolling pulls and the 65 deg releases identical to 2.5.11 (the bench's yaw there is not saturated, so the derate does not engage - and the bench replay of 53.5-59.5 s makes 4 deg where the game made 11.1; this is the known bench weak spot).

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.5.12; `BepInEx\plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 2.5.12. Backup `.dll.2.5.11.bak`. Release `C:\Games\NO mods\F22E-FCS-2.5.12-release`. LogOutput: `F-22E FCS 2.5.12`.
