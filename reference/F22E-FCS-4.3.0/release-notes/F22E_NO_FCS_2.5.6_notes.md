# F-22E FCS 2.5.6

Base: **2.5.5**. One fix from the 10:43 log (2.5.5, level 3): the falling-leaf reversal.

## What the 10:43 log said
At 65 deg AoA / 85 m/s (80 s of the flight above 50 deg AoA) a full lateral-stick reversal took **3.4 s** to swing the yaw-rate command from -43 to +19 deg/s (220 s; 270.8 s the same the other way), with the stabs near neutral the whole time (-13..+5 deg) - the command moved so slowly that the jet was never far from it. Cause: the pace governor's open-loop cap takes the yaw the *rudders* can add from their tables; past the rudder fade (`RudderFadeStartAoA` 50 - `End` 60) those tables say "no yaw" and the cap sat on its 20 deg/s^2 floor (`latAcc` 20 throughout). 2.5.2 level 3 ran the trajectory there at 60-95 deg/s^2.

## Change
- The open-loop pace cap hands over to the one-axis law as the rudders fade: `cap = lerp(no cap, rudder cap, rudderAuth)`. Below 50 deg AoA nothing changes (the 40-48 deg rolling pulls the bench cleaned keep their cap); above 60 the trajectory runs at the tuned acceleration / braking and your yaw-rate braking floors (`HighAoABrakeFloor`, `YawStopTime*`) pace it as before 2.5.3. Static `PaceRudderFade` (1 = this, 0 = 2.5.5) for A/B on the bench.
- Correction to the 2.5.3-2.5.5 notes: `CoordinatedStop` is **off** in your config (and defaults to false), so "kept at every level" was true of the code and idle in flight; the released-stick high-AoA stops have been the pace window alone.

## Bench (calibrated plant; its yaw at 65 deg AoA is faster than the game's, so the absolute times are low)
Replays of the two 10:43 reversals with your sticks, time for the yaw rate to reach half its old value the other way: 2.5.5 cap 2.52 / 2.67 s -> **2.5.6 1.72 / 1.65 s**, the same as with the pace governor off (1.75 / 1.65); game with 2.5.5: 3.55 / 3.70 s. Sideslip at the reversal: 1.8 / 2.9 -> 3.1 / 3.0 (game 1.9 / 2.3; no pace 4.1 / 3.0). The stabs go to their stops at once in all bench variants (the game's stayed near neutral under the 2.5.5 cap). The 65 deg AoA release scenarios move 3.5 -> 3.8 / 3.7 deg (the cap no longer paces them); every other regression scenario and the stop / reversal sweep: identical to 2.5.5.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.5.6. Backup: 2.5.5 is `...\Aryx_F22E_StrikeRaptor_FCS.dll.2.5.5.bak`. Release: `C:\Games\NO mods\F22E-FCS-2.5.6-release`. No new config keys. LogOutput: `F-22E FCS 2.5.6`.
