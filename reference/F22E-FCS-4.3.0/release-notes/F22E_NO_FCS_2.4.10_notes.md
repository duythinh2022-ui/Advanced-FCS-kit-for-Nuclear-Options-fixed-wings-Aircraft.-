# F-22E FCS 2.4.10

Base: **2.4.9** (2.4.8 + `WingCamberCoupling` 0.3 + `MaxPitchRate` 60). This 2.4.10 replaces the earlier experimental 2.4.10, which is kept as **2.4.10-beta**.

One change: the ground steering pedal now matches stock. Flight laws are untouched.

Compiled and bench only. Not taxied.

## What changed
The fly-by-wire steering stays on (`NoseWheelFbw`). The pedal now asks for the yaw rate the stock steering would give.
- **Stock wheel:** pedal × steering lock, slewed at the gear's steering speed. Both are read from the nose leg at runtime; 45° and 60 deg/s on both jets.
- **Kinematic yaw rate:** V·tan(wheel) / wheelbase.
- **Tyre cap:** where the nose tyre saturates, the cap is a lateral acceleration of nose friction × g. The nose friction is also read from the leg: 1.0 on the F-22E, 0.7 on the Ifrit.
- **Removed limits:** no `GroundYawRateMax` (15 deg/s), no `GroundLateralAccel` (2.5 m/s²) and no 20 deg/s² yaw-acceleration limit on the pedal.
- **Rudders:** they follow the pedal as stock, and the yaw-rate loop trims on top in the ground roll.
- **Pedal released:** unchanged. Yaw braking, then the straight-line hold and the touchdown crab correction.

New dev key: **`GroundStockMatch`** (Ground, `true`). Set it to `false` to get the 2.4.9 capped law back.

## Why the old law felt sluggish
The game's `LandingGear` sets the wheel to pedal × lock (read from the IL).
- **At speed:** with the tyre saturated, a steady stock turn pulls about μg sideways whatever the CG, because the nose and main forces balance in moment.
- **What 2.4.9 allowed:** 2.5 m/s². That is a quarter of the F-22E's stock 9.8 m/s² (nose μ 1.0).

## Bench
A yaw-plane model of `LandingGear` (tyre law, castor, slew, differential brake) was calibrated on the 27 Oct Ifrit taxi log. It replays the logged FBW wheel commands to within 0.7–0.8 deg/s RMS of the logged yaw rate.

F-22E figures, yaw rate 2 s after a pedal step (deg/s):

| Speed | Pedal | Stock | 2.4.9 | 2.4.10 |
|---|---|---|---|---|
| 10 kt | full | 27.4 | 15.1 | 27.4 |
| 23 kt | full | 29.2 | 12.1 | 29.2 |
| 39 kt | full | 23.9 | 7.3 | 23.9 |
| 39 kt | 0.2 | 24.2 | 1.5 | 23.9 |
| 68 kt | full | 15.8 | 4.2 | 15.8 |

- **Rise time** (to 63 % of the turn rate): within 0.02–0.1 s of stock.
- **Stopping the yaw** after release: 0–0.2 s slower than stock at full pedal, faster at part pedal.
- **Full table:** `bench/groundsim/results.txt` in the source zip.

Caveats:
- **F-22E plant is assumed.** Mass 25 t, 18 % of the weight on the nose, Iz = m × 4.6². None of the logs has F-22E pedal input on the ground, so it is not calibrated on its own log.
- **No rudder aero in the bench.** At takeoff speeds the stock rudders add yaw on top of the tyre. The 2.4.10 cap stays at the tyre value, so above about 60 kt full pedal may turn a little slower than stock.
- **Stock is sensitive at speed.** On both stock and 2.4.10, 20 % pedal already saturates the nose tyre above about 30 kt.

## Found in Aryx's gear data (not changed)
Both F-22E main legs have `differentialBrakeFactor` = −1. So between 6 and 58 kt, left pedal adds 0.3 × pedal brake on **both** mains, and right pedal adds none. The F-22E has no stock differential-brake steering, only a left-pedal drag. The Ifrit's mains are −1 / +1, as intended.

## Why the logs could not be used for stock
`Enabled = false` switches off the whole FCS, including its logger. Your Ifrit settings file was saved at 10:56:38, the second the 10:55:34 log ends. Every logged ground row is FBW.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 375785060087b87174f92498cf9a3cd0) and `meta.json` 2.4.10.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` (6886d48319d5842b7e39a2b8dfc4d8a7).
- **Backup:** 2.4.9 is `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.9-stable.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.10-release` (replaces the earlier 2.4.10 folder; that build is in `F22E-FCS-2.4.10-beta-release`).
