# F-22E FCS 2.5.11

Base: **2.5.10**. The pace governor's two laws become a blend instead of a switch, as asked. Bench only.

## Change
- **`RollPaceRateBlend`** (Dev, "3. Lateral-directional", default 1.0, 0-1, takes effect at once; `RollPaceRateMode` false forces 0). 0 = 2.5.7 pacing only (window + acceleration cap). 1 = 2.5.8 rate cap only. Between: the roll-rate target is the proportional mix of the two laws' targets (the window's hold and the rate cap's trim each scaled by their share) and the trajectory's acceleration cap gives way to the tuned acceleration / braking in the same proportion. `RollPaceRateSlip` still scales the rate cap's margin. `paceLead` logs the rate cap above 0.5, the window's yaw reach below.
- Bench reversal sweep (27 cases, 450-700 km/h, 10-29 deg AoA, margin 15), worst / mean sideslip, mean roll, mean reversal time:

| blend | worst / mean | roll | reversal |
|---|---|---|---|
| 0 (= 2.5.7) | 5.1 / 3.1 | 59 | 0.56 s, 6 of 9 moderate-AoA cases not completed in 1.3 s |
| 0.5 | 9.5 / 6.4 | 71 | 0.47 s, 2 not completed |
| 1 (= 2.5.8) | 5.9 / 4.0 | 52 | 0.39 s, all completed |

  The middle is **not** between the ends in sideslip: each law is only half-holding, so the roll runs faster than either (565 km/h / 19.5 deg AoA: 51 deg/s against 34 paced and 41 capped) and pays in sideslip (9.5 there). It is a third setting, not an average. Expect the game's worst reversal at ~1.4x the bench's.
- Dev-unlock DLL version string 2.5.11.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.5.11; `BepInEx\plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 2.5.11. Backup `.dll.2.5.10.bak`. Release `C:\Games\NO mods\F22E-FCS-2.5.11-release`. LogOutput: `F-22E FCS 2.5.11`.
