# FS-12 Revoker FCS 1.6.3: hinge copies parked under a replacement model; Typhoon panel posing moved to its own DLL

Base: 1.6.2. Patch: visual only. The flying is identical to 1.6.2 and 1.6.1: the control laws, the allocator, the surface commands, and the game's surface transforms (which are also where each surface's lift acts) are all unchanged.

**Status:** compiled; the bench builds. **Not checked in game.**

## Tom (24 Nov 01:57)
"not fixed, the outer pair still floats, maybe make the separate dll to fix it"

## What 1.6.2 showed
Tom's 01:56 log: the panel posing ran on 7 panels, but chose `aileron_*_visible` for the inner flaperons (nearest pivot was the wrong rule), and the outer pair still floated. Posing the outer panels by the aileron, from the game's resting pose and with known moving Revoker surfaces held, did not stop it. So what moves them is something else above them in the hierarchy.

**The likely cause** (to be confirmed by the new DLL's log): the FCS's own corrected-hinge aileron copy (`aileron_*_visible_hingeVis`, 0.2.3).
- It is a mesh renderer sitting where the Revoker's aileron is.
- The Typhoon mod hangs each of its panels under the smallest base mesh around it.
- 1.6.1 hid the copy, but kept turning it about the Revoker's hinge every frame. A Typhoon panel hung under it was carried along: turned twice, about a pivot almost a metre forward of its own.
- That matches "fine with the FCS off": without the FCS there is no copy.

## Changes
1. **`VisualHinge`:** once the base model is hidden by another mod, each copy is parked at the aileron's resting pose and no longer moves. Anything hung under it stays put.
2. **The Typhoon panel posing (`ModelSwapSurfaces`, 1.6.2) is removed from the FCS.** It now lives in its own plugin, `RevokerTyphoonCompat.dll` 1.0.0, as Tom asked. The dev key `ReplacementModelSurfaceFix` is gone with it.
