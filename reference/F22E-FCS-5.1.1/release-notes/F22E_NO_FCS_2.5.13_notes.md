# F-22E FCS 2.5.13

Base: **2.5.12**. The shed cap gets the same yaw derating the rate cap got in 2.5.12. From the 13:42 log (2.5.12, `RollPaceRateBlend` 0.41 - the F1 slider's "50 %"; Tom: "I like everything as it is, except the sideslip spike at the end"). Flown-only.

## What the 13:42 log showed
223 s; sideslip max 13.5, q95 4.6, 7.6 s above 5 deg, 1.0 s above 8. The 13.5 at 218.9 s: full lateral at 64 deg AoA / 125 m/s (target -62, body yaw -62, `yawSat` 0 - the axis was delivering, the 2.5.12 derate correctly idle); at 216.8 s roll released and full push together; the yaw command flipped -58 -> -4, the axis shed ~35 deg/s^2 with `yawSat` 1.0 while the table said 70-160; the push took the AoA 64 -> 0 in 1.2 s (q -48) with 27-30 deg/s of yaw still on, and that became 13.5 deg at the zero crossing. The shed cap was engaged (`yawSatPitch` -25 to -61) but sized the yaw's shedding from the table: 0.6 x 27 x 69 / 23 = 48 deg/s of AoA rate allowed at 27 deg AoA. Other excursions: 58 s 7.6 (same family, full push at 65 deg AoA), 99 / 148 s 6.7 / 7.4 (brief), 168 s 7.2 (rolling reversal at 22 deg AoA / 167 m/s, rudders 0.67-0.89 - the blend's price).

## Change
- Shed cap: the yaw authority it divides by is `aYaw` x (1 - derate x `yawSat`), derate `PaceSatDerateHi` 0.5 past the rudder fade / `PaceSatDerateLo` 0.25 below (as the rate cap). At `yawSat` 1.0 the 217 s push would have been held to ~40 deg/s of AoA rate at 55 deg AoA, 33 at 40, 25 at 27, 12 at 15 - 64 -> 15 deg in ~2 s instead of 1.2, the yaw gone before zero at the 35 deg/s^2 the axis was really giving. Nothing changes where the yaw is not saturated.
- Dev-unlock DLL version string 2.5.13. No new keys.

## Bench
Regression at blend 0.41 identical with and without (push + roll 200 4.2 / 4.1, pull-throughs 6.1 / 5.6, 65 deg releases 3.1-3.2): the bench's yaw does not saturate in these, so the derate does not engage there. Flown-only.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.5.13; `BepInEx\plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 2.5.13. Backup `.dll.2.5.12.bak`. Release `C:\Games\NO mods\F22E-FCS-2.5.13-release`. LogOutput: `F-22E FCS 2.5.13`.
