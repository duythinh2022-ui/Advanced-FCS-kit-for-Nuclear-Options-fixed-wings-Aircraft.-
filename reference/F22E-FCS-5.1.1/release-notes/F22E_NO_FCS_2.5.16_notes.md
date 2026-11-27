# F-22E FCS 2.5.16

Base: **2.5.15**. One correction to the probe from the 14:55 log (2.5.15 flown, 420 s). Flight-derived.

## What the 14:55 log showed (2.5.15, blend 0.41, margin 4)
- Sideslip max 13.5, q95 5.1, q99 7.8; above 8 deg 3.6 s, above 10 deg 1.1 s, of 402 s airborne. By AoA band (q95 / max): 0-15 deg 3.7-5.1 / 8.9-10.5; 15-25 5.4 / 10.5; 25-35 7.5 / 13.5; 35-50 4.6 / 8.4; 50-70 4.7 / 6.6. The five >8 deg events are full-stick rolls or reversals at 20-35 deg AoA, one roll stop under a full pull at 181 m/s (13.5), one full push + roll at 1 deg AoA / 90 m/s (8.9). Tom: slight sway, small, "a good cost for having crispier roll".
- Reversals (stick to roll zero crossing): 0-15 deg AoA 0.29 s mean, 15-22 0.34, **22-32 0.16**, 32+ 0.48; sideslip peaks 2.9 / 4.9 / 6.8 / 3.9 mean.
- Low-AoA full-stick rolls (2.5.14's trade fix): `pSmax` 104 / 138 / 163 at 85-100 / 100-115 / 115-130 m/s against 66 / 102 / 158 before; the trade's limit now sits at `pMax`.
- Two push-through corkscrews from 46 and 64 deg AoA to -30, 2.7 / 3.1 s, pitch rate to -54 / -61 deg/s with no steps (|q-dot| max 116 deg/s^2), sideslip peaks 8.4 / 8.9, yaw at the zero crossing 5 and 16 deg/s with the shed cap engaged (-53 / -57). Tom: "ran great".
- **Probe:** `slipStiff` in the live airframe +2.9 deg/s^2/deg at 0-20 deg AoA, +1.7 at 20-30, +0.6 at 30-40, ~0 past 40 (the bench plant: +2.7 / +1.9 / +0.2 / -0.2 - same shape). `slipTol` 25 everywhere except brief readings of 12 at 50-70 deg AoA (0.7 x 12 = 8.4 > 4, no effect) and **4 at -20..-60 deg AoA** (2 s in total). The latter is an artifact: past the rudder fade the test compared the airframe's destabilizing moment against half of the *rudders'* yaw, which the tables fade to nothing there, so any destabilizing moment at all tripped it. The margin never moved (short episodes, 0.5 s filter), but on a craft that lives at negative AoA it would have.

## Change
- The departure test holds against the yaw axis the allocator actually has: the rudders' yaw at that sideslip, or past the rudder fade the allocator's yaw authority (`aYaw` x (1 - `rudderAuth`)), whichever is larger - the same handover the rate cap and the shed cap use.
- Dev-unlock DLL version string 2.5.16. No new keys.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.5.16; `BepInEx\plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 2.5.16. Backup `.dll.2.5.15.bak`. Release `C:\Games\NO mods\F22E-FCS-2.5.16-release`. LogOutput: `F-22E FCS 2.5.16`.
