# Su-30VXN FCS 0.6.1 (16 Nov 2026): AoA limits +80 / -75 by default

Base: 0.6.0. Patch bump: config defaults only, no law change.

Tom, 16 Nov 09:55 (after flying +80 / -75 from his dev file in the 15 Nov 22:08 log): "just update the flanker to +80/-75".

## Change
- **`AoALimitPositive`** (dev, `2. Envelope`): default 75 -> 80.
- **`AoALimitNegative`** (dev, `2. Envelope`): default 65 -> 75.
- **Migration:** a dev file still holding the old default is moved once (`Su30DevRevision` 15 -> 16). A value set by hand is kept.
  - Your file already holds 80 / 75 and is not touched.
  - LogOutput shows "Su-30 FCS 0.6.1 defaults moved once: (none held the old values)."
- **Unchanged:** the no-TVC / low-thrust caps (NoTvcAoACapPositive / Negative 50), the soft limit, MPO.

## Not benched
- These are the limits you have flown since the 15 Nov log, so your jet flies exactly as before.
- **What I found in that log (for the record):**
  - 38 stick-released yaw stops at the +80 limit: median deceleration 21 deg/s^2, about 2.1 s from 55 deg/s, median peak sideslip 6.7 deg.
  - The pace comes from the released-stop allocator weights (velocity roll back to ~1.5-2.7 against sideslip 8), not from the limit.
  - Not changed here; see project doc `claude/Su30_Revoker_yaw_stop_logs_2026-11-16.md`.
- **Older bench (0.1.0, for reference):** 80 did not hold below ~20 m/s, and rolls there reached 31 deg of sideslip.

## Status
- **Compiled** against the game's assemblies. Built twice, same bytes: 1,527,296 bytes, SHA-1 3d59241be625b7254ca3d3b340d00f2c86e1f1e4, md5 842500f5b5af8af90c0b9f5d9cc46480.
- **Source check:** the 0.6.0 source rebuilt here gives the released 0.6.0 DLL byte for byte.
- **Not flown** as 0.6.1 (the same limits were flown from the dev file).

## Install
- **Main:** `BepInEx\plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.6.1. 0.6.0 is kept as `Su30_VXN_FCS.dll.0.6.0.bak`. `meta.json` updated.
- **Dev DLL:** unchanged.
- **Release folder:** `C:\Games\NO mods\Su30-FCS-0.6.1-release`.
- **Mod manager:** update its record, or its modpack may put 0.6.0 back. Restart the game to load it.
