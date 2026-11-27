# F-22E FCS 2.6.6

Base: **2.6.5**. One change: the shed cap no longer stalls the nose at the zero crossing. Bench + the cap re-evaluated on the logged hitch, not flown.

## What the 22:49 log said (2.6.5)
Six push-overs under roll. The hitch is the **shed cap at the crossing**: 239.0-239.4 s (165 kt, 65 deg corkscrew, full push + full roll): pitch command -46 → -3 as the AoA went 2 → 0.3 deg, pitch rate -45 → -16, then back to -24 past zero. The 2.5.8 shed cap says "the sweep may not reach zero sooner than the excess yaw can be shed" - time-to-zero → 0 as |AoA| → 0 at any rate, so the last degrees always run at the 6 deg/s floor while any excess (5-8 deg/s of yaw here, after the 5 deg/s dead band) is left. The hold shed 3 deg/s of yaw in 0.3 s and the sideslip went 4.1 → 5.4 through the crossing regardless: near zero AoA the sideslip accrues at the full excess whether the nose waits at 2 deg or passes through.
- The other low-speed push-overs (213.8 s, 118 kt, 65 → 6 deg at 20-30 deg/s; 123.9 s was you easing the stick) are **not** the shed cap: they are the sweep cap - 0.5 of the yaw authority (+ half the roll's) over the roll rate - and at 118 kt the authorities are 27-33 / 23-66 deg/s², so the push is paced to ~30 deg/s. Sideslip 1.5. That is the governor's capacity rule doing what it says; unchanged here.

## Change
Holding the sweep at AoA a while the yaw sheds is worth e·(1 − cos a) of sideslip rate against going on to zero: half the excess at 60 deg, an eighth at 30, nothing at 10. So the cap's pace is weighted by that worth, `(1 − cos AoA)` against its value at 60 deg (`SweepShedCos` 0.5; 1 = the 2.5.8 cap at every AoA): the cap stands at 60 deg and above where the rotation has to be shed, eases through 45-30 where waiting buys little, and frees below ~15 deg where it buys nothing. The 2.5.8 floor of 5 deg/s under the excess, which kept the cap singular whatever the weight, is gone: below 0.5 deg/s of weighted excess the cap is off. Re-evaluated on the logged 239.0-239.4 s window: old cap 70 → 6 deg/s over the last 2 deg, new cap 60 throughout.

## Cost, stated plainly
The one case the shed cap was built for - a 71 deg/s post-stall rotation pushed through zero at 100 m/s (11:27 log, 203 s: 37 deg in the game, 20 on the bench without the cap, 17 with it) - gets less pacing between 45 and 30 deg AoA: bench replay 16.8 → 21.7 deg, zero AoA reached 0.5 s sooner. The physics says the pacing there was worth about 13-40 % of the excess per second held (cos 30-45 deg), which the bench agrees with; the game reads this corner at ~2× the bench. At 115-135 kt the pushes from a rotation go through faster for the same sideslip (31.9 s replay: -20 → -34 deg at 2.8 s, 7.7 → 7.6) - the wait there was buying nothing, as the 22:17 analysis said.
Suites: corkscrews, sweep, rolling pulls identical to 2.6.5; push + roll 200/300 km/h 3.6/1.4 → 3.2/1.5 with the push 1-4 deg deeper at 2 s; push-then-pull-through 350 2.6 → 3.5.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.6.6; Dev DLL 2.6.6. Backup `.dll.2.6.5.bak`. Release `C:\Games\NO mods\F22E-FCS-2.6.6-release`. No key changes. `yawSatPitch` should no longer deepen in the last 5 deg before zero AoA.
