# FS-12 Revoker FCS 1.6.2: the EF-2000 Typhoon mod's moving panels posed from the FCS's surfaces (visual only)

Base: 1.6.1. Patch: visual only. The control laws, allocator, surface commands, the game's surface transforms (which are also where each surface's lift acts), colliders and damage are all identical to 1.6.1.

**Status:** compiled against the game's assemblies; the bench builds. **Not checked in game.**

## Tom (24 Nov)
- 01:40: "the inner pair pitch up looks fine, but down creates a gap, the outer pair is straight up ufo tech. Look into the skin files and see how it works".
- 01:49: "the gap is gone with the fcs mod disabled".
- 01:51: "make fcs mod function the same, but visually doesnt make the gap".

## How the Typhoon mod moves its surfaces (read from TyphoonMod 1.0.0: `typhoon_surfaces.txt` and the plugin's code)
- **Hinges and drivers:** each panel has its own hinge line (two points) and one or more Revoker surfaces as drivers:
  - inner flaperon `aileron_*_visible+flap_*_visible`
  - outer flaperon `aileron_*_visible`
  - canard, rudder, airbrake, nozzles and cockpit controls likewise
- **Rest pose:** it saves each driver's local rotation at the moment it converts the jet. This is not the game's resting pose.
- **Every frame:** it adds up each driver's rotation from that saved pose, projected on the panel's hinge, clamps the sum to ±45 deg and sets the panel's hinge rotation.
- **Placement:** each panel hangs under the smallest Revoker mesh around it (by bounds), which can be a moving surface.
- **The hinge lines themselves are right.** Checked against `typhoon.obj`: the leading edges of `flaperon_in_R` / `flaperon_out_R` lie on them within 1 cm.

**Why it gaps only with the FCS.** On the stock Revoker the ailerons only roll (pitch range 0) and the inner flaps are landing flaps only, so aileron + flap stays small. Under the FCS both are elevons. Your 01:38 log shows aileron 30 deg and flap 25 deg at full stick, so the inner panel was asked for 55 deg and clamped to 45. Trailing edge down, the model has no cover at that angle.

## What 1.6.2 does (`ModelSwapSurfaces`, dev key `5. Airframe / ReplacementModelSurfaceFix`, default on)
Once the Typhoon mod has built its panels (looked for twice a second for up to 60 s after spawn), each panel driven by Revoker surfaces is re-posed every frame. This happens after that mod's own update and before rendering (`Application.onBeforeRender`).

1. **One driver per panel:** the Revoker surface nearest the panel. Inner flaperon <- inner flap; outer flaperon <- aileron; canard <- canard; rudder <- rudder.
2. **Angle:** that surface's deflection from the game's resting pose (the control surface's own resting rotation; the inner flaps at their modelled pose). It is projected on the panel's hinge and clamped to the mod's ±45 deg.
3. **Placement:** the panel turns about its hinge as placed at conversion. If a moving Revoker surface sits above it in the hierarchy, that surface is taken at its conversion pose, so the panel does not turn twice. Otherwise only the panel's hinge rotation is set, exactly as the mod sets it.

**Left to the mod's own update:** the airbrake, nozzles, cockpit stick, pedals, throttle, switches and gear.

**What is not changed:** the Typhoon mod's files and code. The FCS only sets its panels' transforms, after it, at render time.

**Log on spawn:** `Revoker FCS replacement-model surfaces (TyphoonVisual): N panels posed from the FCS's surfaces at render time (visual only): Typhoon_flaperon_in_R <- flap_R_visible; ...`.

**Not known yet: the outer flaperons' "ufo" look.** The cause could not be pinned down from the files. It is either an offset from the saved conversion pose or a double turn under a moving Revoker part. 1.6.2 removes both. If the outer pair still floats, that is a third cause, and the log line above plus a screenshot is the next step.

## Kept from 1.6.1
The FCS's own corrected-hinge aileron copies hide when the base Revoker meshes are hidden. Your 01:38 log shows 75 of 85 off, both wingtips off, copies hidden.
