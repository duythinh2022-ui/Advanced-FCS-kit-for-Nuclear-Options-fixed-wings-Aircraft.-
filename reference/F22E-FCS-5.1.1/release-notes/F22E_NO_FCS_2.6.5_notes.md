# F-22E FCS 2.6.5

Base: **2.6.4**. One correction to 2.6.4's trade split. Bench + log replay, not flown.

## What the 22:33 log said (2.6.4)
- "Rolling at low to moderate AoA is much slower." Stick-centered full-stick rolls at 20-42 deg AoA (51-55, 175-180, 255-260, 352-362 s): the trade's new pitch share was active (`tradeAlpha` 6-15, `yawSatPitch` -2..-5) with the pitch stick at zero. The AoA/G law's return toward 1 g under a roll is a nose-down sweep, and 2.6.4 applied the pitch share to it: the unload was held to 8-15 deg/s of AoA rate (pitch command kept 2-5 deg/s above its ask through 42 -> 15 deg), so the jet stayed at high AoA longer and rolled at the high-AoA rate. Replay of 52-55.5 s: 2.6.3 roll 71 deg/s mean, AoA to 6; 2.6.4 61 deg/s, AoA to 10.
- "The stabs chatter more when rolling." Not found in the numbers: stabilator command content above 2 Hz in rolls at 15-30 deg AoA 1.95 deg rms (2.6.3: 2.09, 2.6.1: 2.53), at 30-50 deg 3.46 (2.68 / 2.70 / 2.5.15: 4.27); pitch-command content lower than both. What is there is the rudder command's 2.8-3.8 Hz jitter, 3-7 deg rms in every version since at least 2.5.15 (the actuator smooths it to 1.2-2 deg) - the yaw loop's own, not the governor's. If the chatter you feel is new, say where in the log and I'll look at that spot.

## Change
The trade's pitch share acts only on the sweep that **grows** the yaw demand (the pull at positive AoA, the push at negative), never on the one that shrinks it (the unload at positive AoA, the pull from negative). A falling |AoA| under a roll takes yaw demand away, which the rudders serve by unloading - there is no free-yaw cost to split. `yawSatAlphaRateDn` goes back to the sweep / shed caps alone. One line; the pull-side split, the 2.6.3 forecast and everything else as in 2.6.4.

## Bench, 2.6.4 -> 2.6.5
- Replay 52-55.5 s: roll 61 -> 71 deg/s, AoA to 6 (= 2.6.3), sideslip 1.6 -> 1.9.
- Corkscrews (20), rolling pulls, sweep: identical to 2.6.4 (the pull side is untouched): corkscrew sideslip 3.10, roll 45; rolling pulls 4.2/3.7/4.0; stops 2.62, reversals 3.69, crossings 0.39.
- Regression: identical except the push-then-pull-through pair at 350 km/h (3.5 / 2.6 vs 2.6 / 2.8 - the pull-through from negative AoA now runs untraded through zero), AoA at 2 s -26 vs -24.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.6.5; Dev DLL 2.6.5. Backup `.dll.2.6.4.bak`. Release `C:\Games\NO mods\F22E-FCS-2.6.5-release`. No key changes. In the log `tradeAlpha` should read 0 whenever the pitch stick is centered and the AoA is falling.
