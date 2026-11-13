# Ifrit FCS 0.7.2

Base: **0.7.1**. The reason the governor pinned your sideslip at 2-4 deg of an 8 deg margin, and the fix. Patch bump: one correction inside the rate cap plus a default change.

**Bench-only.**

## 1. The rate cap was capping on a reversal the governor would never fly
You asked why the governor pins the sideslip so low when it is allowed a few degrees. Your 01:04 log answers it: `revSimBeta` (the model's own prediction, new in 0.7.1) reads **7.4-7.9 deg in every AoA band** - the bisection always lands exactly on the margin, as designed - while the sideslip you actually make is **2.2-3.9**. The model over-predicted by 2-3.5x, so the cap was 2-3x too tight, and the cliff you feel past 16 deg AoA is where it starts binding (`rateCap` 108-109 at 10-22 deg, **43 at 22-30, 34 at 30-40**).

The model's internal trace at 30 deg AoA / 150 km/h shows why: it reversed the demand at **128 deg/s^2** and asked the yaw for 150-200 deg/s^2 while the airframe delivered 20 -> 11 deg of predicted sideslip. The real jet never does that, because **the governor's own open-loop acceleration cap paces the reversal to 27-53 deg/s^2**. The model simulated an *unpaced* full-stick reversal and the governor then capped the rate on that answer - double-counting its own protection.

Fixed: the model's reversal acceleration is now bounded by the pacing the governor will actually apply (`PaceYawUse` of the yaw the effectors can add, over sin AoA - the same expression as the open-loop cap, for the weaker side). With the error gone the model predicts 0.04-0.18 deg on this airframe and the cap sits at its ceiling.

## 2. ...which is the finding: the cap has no work to do on this jet (`RollPaceRateBlend` default 0.4 -> 0)
With the cap honest and inert, **blend 0.4 does not mean "window only" - it dilutes the yaw-reach window's hold to 60 %**, which is strictly worse. Bench, your config at margin 8:

| | stop W / mean | reversal W / mean | sweep roll | 20-25 deg AoA full-stick roll |
|---|---|---|---|---|
| 0.7.0 (what you flew) | 6.0 / 3.61 | 6.0 / 3.94 | 101 | 57-58 deg/s at 4.4-4.6 deg |
| 0.7.2 at blend 0.4 | 6.8 / 3.63 | 7.6 / 4.68 | 106 | - |
| **0.7.2 shipped (blend 0)** | **4.9 / 2.69** | **5.8 / 3.22** | **101** | **62-65 deg/s at 1.3-1.6 deg** |
| 0.5.6 old stack | 5.5 / 3.39 | 7.2 / 3.73 | 109 | - |

Best on every axis, and better than the old stack on both. A config file still holding exactly the old 0.4 default is moved to 0 once (`GovernorRevision` 2); if you have set it by hand it is kept. Corkscrews 39-44 deg/s at 1.0-4.7 (0.7.0: 39-43 at 1.0-4.9); falling leaf 30-43 deg/s at 2.5-5.3 deg.

## 3. What is still slow above 16 deg, and it is not the governor
In your own log the **authority**-based rate limit `pSmax` falls from **86 deg/s at 16-22 deg AoA to 65 at 22-30** - that is the Ifrit's roll authority as the wing goes, not a governor limit (`RollAuthorityUse * pFull * aRollGo/aRoll`). The governor's remaining contribution in that band is the window pacing the entry, 5-15 deg/s of hold. If you want more there it is an airframe/allocator question (roll authority at 20-30 deg AoA), not a governor one.

For reference, this is also why none of it happens on the Raptor: the Raptor is still 2.6.6 and never got this cap at all, and its yaw axis really is twin rudders, so the old kinematic formula's premise holds there. The Ifrit has no rudders; its yaw above ~20 deg comes from the trailing edge.

## Install
- **Main:** `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 564d98db47d432e0077e124b1158f087), version 0.7.2.
- **Backup:** 0.7.1 kept as `…\Ifrit_KR67_FCS.dll.0.7.1.bak` (0.7.0, 0.6.0 and 0.5.6 also still there).
- A/B statics: `RevSim` false = the kinematic cap, `PaceReachAll` false = 0.7.0's window reach, `SlipFrameAlloc` false = the body-frame allocator. Player knob: `RollPaceRateBlend` back to 0.4 restores the blend you flew.
