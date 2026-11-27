# Ifrit FCS 0.7.1

Base: **0.7.0**. From your 01:48 flight log. Patch bump: no new control law, two corrections inside the governor plus the telemetry that makes the remaining error measurable.

**Bench-only.** Read §3 — there is a knob to set, and it is worth more than anything in this build.

## What your log says (0.6.0 -> 0.7.0, full lateral stick, same AoA bands)
0.7.0 is measurably faster than 0.6.0 everywhere, so "too slow" is not a regression against what you flew before:

| AoA | abs pS q90 | abs p q90 | abs r q90 | governor's hold | beta q95 |
|---|---|---|---|---|---|
| 15-25 | 49 -> **92** | 47 -> 87 | 20 -> 30 | 18.3 -> 13.2 | 3.6 -> 4.9 |
| 20-30 | 62 -> **76** | 55 -> 70 | 29 -> 28 | 18.6 -> 12.1 | 3.8 -> 4.4 |
| 25-35 | 62 -> **64** | 55 -> 52 | 34 -> 38 | 23.6 -> 11.7 | 2.8 -> 2.3 |
| 45-60 | 37 -> **53** | 26 -> 35 | 26 -> 39 | 12.8 -> 1.2 | 2.4 -> 5.9 |
| falling leaf (a>55, full aft) | r max 46.8 -> **60.9** | | | | 1.2 -> 3.0 |

But you are right that it is tight: the governor is still taking **11.7-14.1 deg/s** off the stick at 10-45 deg AoA while the sideslip sits at 2.3-4.9 deg of an 8 deg margin.

## 1. The rate cap's modelled reversal was too violent (fixed)
The forward model stepped straight to the trajectory's acceleration *limit* (`RevFloorK * aRoll / cos a` = 127 deg/s^2 at 150 km/h / 35 deg AoA) where the law, jerk-limited, actually realises **27-35**. It now ramps at the lateral law's own jerk limit (`RollJerkTau`, `RollJerkServo` - the `a2` of the roll trajectory). Worth ~1-2 deg/s of cap in your band; it is a correctness fix, not the fix.

## 2. The yaw-reach window reads every effector (`PaceReachAll`, on)
The window holds the commanded roll to what the measured yaw can follow inside `PaceHorizon`, and it was computing its reach from the same hand-picked yaw set the rate cap used to use - which leaves out the trailing edge, the jet's real yaw axis above ~20 deg AoA (aileron pair +-12.5 deg/s^2 at 150 km/h / 65 deg against the tails' +-2.2). Now every allocator effector, each from where it is at its own slew inside its allocator window. In flight the reach read 5-6 deg/s at 15-45 deg; on the bench it goes to 5.3-6.8. **Also small** - because the horizon is 0.15 s and no airframe adds much yaw rate in 0.15 s. The crossing time, the probe's authority and the yaw slew lead keep the dedicated set. `false` = 0.7.0's reach.

## 3. The real problem, and the knob (do this)
**The rate cap earns nothing on this jet and costs rate.** With the cap out of the blend (`RollPaceRateBlend` **0**, player config, section 6, takes effect at once) the bench gives, at the same AoA:

| | shipped (blend 0.4) | blend 0 |
|---|---|---|
| 150 km/h, 40 deg AoA, full stick | 49 deg/s at 3.0 deg | **57 deg/s at 1.2 deg** |
| 200 km/h, 31 deg | 59 at 2.9 | **63 at 1.7** |
| stop/reversal sweep worst | 6.0 / 6.0 | **4.9 / 5.8** |
| sweep mean sideslip | 3.61 / 3.94 | **2.69 / 3.22** |
| falling leaf 65 deg | 29-42 | 30-43 (same) |

More rate **and** less sideslip, everywhere. I have not changed your 0.4 - it is your value, and on the Revoker the cap is worth having (its 70 deg corkscrew goes 66 -> 82 deg/s with the cap in). Set it to 0 on the Ifrit and fly it.

## 4. Why the cap is wrong here, measured (open item)
New telemetry `rateCap`, `revSimBeta`, `revSimT`, `paceReach`. On the bench at 31 deg AoA the model predicts **7.6-7.9 deg** of sideslip for a reversal at its cap of 33 deg/s - right at the 8 deg margin, as designed - while the plant's actual full reversal at **42** deg/s makes **1.9 deg**. So the model is about **4x pessimistic** on this jet at moderate AoA. (With 0.6.0's body-frame allocator it was 4x *optimistic*; the allocator fix flipped the sign of the error.) Suspects, in order: the 3-sweep Gauss-Seidel allocation is too crude to represent what the real allocator achieves; pitch held at zero in the model costs lateral authority the real jet spends differently; the modelled demand profile is still harsher than the trajectory's. `revSimBeta` is now in the flight log, so the next log quantifies it against what you actually make.

## Install
- **Main:** `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 ad5b09ecde2916e9a31cdb6b61a8afca), version 0.7.1.
- **Backup:** 0.7.0 kept as `…\Ifrit_KR67_FCS.dll.0.7.0.bak` (0.6.0 and 0.5.6 also still there).
- A/B statics: `PaceReachAll` false = 0.7.0's window reach, `RevSim` false = the kinematic cap, `SlipFrameAlloc` false = the body-frame allocator.
