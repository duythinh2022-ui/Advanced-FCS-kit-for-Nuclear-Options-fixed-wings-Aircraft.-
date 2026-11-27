# FS-12 Revoker FCS 1.6.4: the Typhoon panel posing folded into the FCS DLL (one DLL)

Base: 1.6.3. Patch: visual only. The flying is identical to 1.6.3, 1.6.2 and 1.6.1.

**Status:** compiled; the bench builds. **Not checked in game.**

## Tom (24 Nov 02:06)
"can both dll be put into 1 single dll"

## Change
The `RevokerTyphoonCompat` 1.0.0 code (Typhoon panel posing) is now inside `Revoker_FS12_FCS.dll` as `PanelPoser` (`ModelSwapSurfaces.cs`). Same logic:
- **One driver per panel:** each panel follows the one Revoker surface under it, picked by the largest overlap in span. The inner flaperon follows the inner flap, the outer flaperon the aileron.
- **Angle:** measured from the game's resting pose and clamped to ±45 deg.
- **Pivot:** the panel turns about its own hinge, with any moving Revoker surface above it held at its conversion pose.
- **When it runs:** at render time, after the Typhoon mod's own update.
- **What it never touches:** any game transform the physics reads, and the Typhoon mod's files.

It is still separate from the flight code. It is added at airframe setup and waits for the Typhoon mod's `TyphoonVisual` on the jet (twice a second, up to 60 s). Kept from 1.6.3: the FCS's hinge copies park at rest under a replacement model.

- **Dev key:** `5. Airframe / ReplacementModelSurfaceFix` (default on).
- **If `RevokerTyphoonCompat.dll` is still installed,** the FCS leaves the posing to it, so the panels are never posed twice. **Delete `BepInEx\plugins\RevokerTyphoonCompat`** to run on the single DLL.
- **Log on spawn:** `Revoker FCS: Typhoon panels on Fighter1: N posed ... <panel> <- <surface> (parent: ...)`. For 6 s after that, one line a second with each panel's angle and its parent's rotation (`Revoker FCS Typhoon panels: ...`).
