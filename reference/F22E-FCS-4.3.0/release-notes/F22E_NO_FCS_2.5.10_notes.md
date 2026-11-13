# F-22E FCS 2.5.10

Base: **2.5.8** (2.5.9's rebuilt estimate is not in this line - it is in `C:\Games\NO mods\F22E-FCS-2.5.9-release` if wanted). One Dev key added, nothing else changes.

## Change
- **`RollPaceRateSlip`** (Dev, "3. Lateral-directional", default 1.0, 0.5-3, takes effect at once): how much of the sideslip margin the rate cap spends - the roll rate / sideslip compromise, in flight. 1.0 = 2.5.8 exactly. Above 1 the cap aims a full stick reversal past the margin and lets the roll run faster. Bench reversal sweep (2.5.8 model, margin 15, 450-700 km/h, 10-29 deg AoA): worst / mean sideslip, mean roll, roll at 700 km/h 10/15/20 deg AoA:

| RollPaceRateSlip | worst / mean | mean roll | 700 km/h |
|---|---|---|---|
| 1.0 | 5.8 / 3.9 | 52 | 116 / 74 / 58 |
| 1.25 | 8.3 / 4.8 | 59 | 133 / 85 / 67 |
| 1.43 | 9.4 / 5.4 | 62 | 144 / 92 / 72 |
| 1.67 | 10.7 / 6.3 | 67 | 157 / 102 / 78 |
| 2.0 | 13.0 / 7.6 | 74 | 170 / 114 / 87 |
| no cap | 10.9 / 8.0 | 83 | 181 / 136 / 103 |

  The game's worst held-roll reversal (rudders pinned) reads about 1.4x the bench's worst; typical ones about half. At 22-29 deg AoA below ~565 km/h the bench's roll is authority-limited at 30-37 deg/s whatever the setting; the 12:07 log showed the cap (35-62 deg/s) was what the roll followed at 20-30 deg AoA / 150-200 m/s in the game, so the knob should act there.
- The Dev-unlock plugin (`Aryx_F22E_StrikeRaptor_FCS_Dev.dll`, in `BepInEx\plugins\`) now carries the FCS version string (2.5.10) instead of 2.4.14. It has no code; only the F1 label and the Dev.cfg header change.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.5.10; `BepInEx\plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 2.5.10 (old one kept as `.dll.2.4.14.bak`). Backup of the FCS: `.dll.2.5.8.bak` (same build as 2.5.8). Release: `C:\Games\NO mods\F22E-FCS-2.5.10-release`. LogOutput: `F-22E FCS 2.5.10`.
