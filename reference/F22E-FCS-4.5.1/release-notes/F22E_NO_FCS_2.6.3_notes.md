# F-22E FCS 2.6.3

Base: **2.6.1** (the flown one). One change: the rolling-pull pitch cap's "sideslip heading for the margin" forecast. Bench + log replays, not flown. 2.6.2 (continuous window) stays built and not installed; this change applies to it too if you pick it later.

## The hitch (21:57 log, 2.6.1)
10 corkscrew entries; 4 had the pitch hitch (28.6, 148.3, 190.6, 215.2 s: 125-148 m/s, pull from 1-6 deg AoA with full roll). It is the pull cap (`yawSatPitch`) and nothing else: the pitch command went 60 → 14-24 deg/s for ~0.4 s at 10-18 deg AoA, pitch rate 44 → 23-32, then back to 60. The roll had already been traded (target cut 30-39 deg/s) and the rudders were at 70-80 %; the sideslip was 1.3-2.1 deg heading out at 7-9 deg/s - the yaw loop 5-6 deg/s behind its own ramp, which is where that rate comes from - and it settled at 2.8-3.4, inside your 4. The cap's forecast was `beta + rate x 0.3 s` = 4.4-5 deg, so it fired at full strength on a sideslip that was never going past the margin. The six entries without the hitch had the rate under 5 deg/s at that moment. The bench corkscrews that fire the cap (450-550 km/h) do reach 3.9-5.2 deg, so there the cap is right; the log replays of your four entries reproduce the hitch (pitch-rate dip 10-14 deg/s on the bench, 12-19 in the game).

## Change
1. **Forecast from the yaw loop's closure** (`StopSlipPitchClose` 1; 0 = 2.6.1). The kinematic sideslip rate is the yaw error × cos AoA; the loop closes that error at the yaw acceleration it is showing now (measured, the pace governor's 0.08 s filter, counted when it is closing the error; never slower than the rudders' braking yaw left), so the sideslip still to come is rate² / (2 × yaw accel × cos AoA), added to the sideslip when the rate runs the same way, taken from it when it is recovering. For your 28.6 s entry: 7.6²/(2 × 25 × 0.98) = 1.2 deg to come, forecast 3.3 against the old 4.4 (actual 2.8).
2. **Fade-in from 75 % of the margin** (`StopSlipPitchStart` 0.5 → 0.75, a 2.5.3 static of mine): the cap is for a pull whose sideslip is going past the margin, not one using it - your rule for rolling pulls. With the honest forecast alone the 28.6 s entry still took a 9 deg/s dip on the replay.

## Replays of the four entries (bench pitch-rate dip / sideslip peak / AoA at 2 s)
| entry | 2.6.1 | forecast only | **2.6.3** | cap off | game (2.6.1) |
|---|---|---|---|---|---|
| 28.6 s | 14 / 3.1 / 33 | 9 / 3.0 / 39 | **3 / 2.9 / 44** | 9 / 3.0 / 38 | 17 / 2.8 / 36 |
| 148.3 s | 11 / 3.7 / 43 | 9 / 3.6 / 47 | **7 / 3.6 / 48** | 12 / 3.6 / 42 | 19 / 3.4 / 35 |
| 190.6 s | 10 / 3.6 / 37 | 5 / 3.5 / 41 | **3 / 3.5 / 42** | 5 / 3.5 / 38 | 12 / 3.2 / 41 |
| 215.2 s | 14 / 3.7 / 29 | 3 / 3.6 / 32 | **2 / 3.6 / 32** | 6 / 3.5 / 31 | 19 / 3.3 / 27 |

Sideslip within 0.2 deg of 2.6.1 in every replay; the pull gets to its AoA 3-11 deg sooner at 2 s. (The replays' residual dip with the cap off is the replay's own warm start, not a second cap.)

## Suites (blend 0.41 / margin 4), 2.6.1 → 2.6.3
- Corkscrews (20): sideslip peak mean 3.66 → 3.76, roll 43 → 43, pitch-rate roughness 13.5 → 14.0 - the cap now only fires where the bench sideslip really reaches the margin (450-550 km/h entries, 3.9-5.2 deg), same strength as before there.
- Sweep (27): identical except one chaotic stop (565 km/h / 20 deg: 2.8 → 6.4; the cap was not active in either run - the runs match to 2.3 s and the stop picks the other rudder side; the bench's known bifurcation at 25-45 deg AoA with the rudders near the stop). Stops mean 2.60 → 2.83 from that one case; reversals 3.66 → 3.71, crossings 0.39 → 0.39.
- Regression (18): identical except the chaotic full-aft reversal (9.1 → 9.2) and pull-then-push-over 450 (3.3 → 3.7).

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.6.3; Dev DLL 2.6.3. Backup `.dll.2.6.1.bak`. Release `C:\Games\NO mods\F22E-FCS-2.6.3-release`. No key changes. LogOutput `F-22E FCS 2.6.3`. In the log: `yawSatPitch` should stay near 0 in entries whose sideslip stays under ~3; it still fires where the sideslip is going past 4.
