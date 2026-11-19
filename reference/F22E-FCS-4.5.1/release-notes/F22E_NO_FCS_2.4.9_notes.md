# F-22E FCS 2.4.9

Base: **2.4.8**. This replaces the earlier experimental 2.4.9 and 2.4.10 builds, which are kept as **2.4.9-beta** and **2.4.10-beta**.

Compiled only. No bench run, not flown.

## Changes vs 2.4.8
| | 2.4.8 | 2.4.9 |
|---|---|---|
| `MaxPitchRate` (dev, 2. Envelope) | 48 | **60** |
| `WingCamberCoupling` (dev, 5. Airframe) | 0 (off) | **0.3** |

Nothing else changed. The source diff against 2.4.8 is these two defaults, the version strings, and the migration below.

## Migration (overrides old dev settings)
- `AirframeRevision` is **21**. Any dev file below 21 gets `MaxPitchRate` = 60 and `WingCamberCoupling` = 0.3 once, whatever it held.
- Revisions 18-20 are skipped on purpose. The beta builds stamped them, so a file they touched is moved as well.
- After the move, values you set are kept.
- Keys that only the betas had (RollEntryCoord, StabWakeEntry, SurfaceCurveShape and so on) are not read by this build and stay in the file unused.
- Without the dev DLL nothing is written: the same two values are the in-memory defaults.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.4.9.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll`.
- **Backup:** 2.4.8 is `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.8.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.9-release`.
