# FS-20 Vortex FCS 0.3.7 (17 Nov 2026)

On 0.3.6. Two changes from Tom's 14:54 flight (short take-off, release guard), plus the max-AoA roll work, which is in development and off. All Vortex-only.

## 1. Max-AoA roll onset (in development; nothing changes in flight)

The first 0.3.7 build (15:14) solved the allocator again from four fixed starting poses above 50° AoA and kept the cheapest. Tom (15:16): no add-ons on top of the allocator. That code has been removed, and the release was rebuilt and reinstalled (15:24).

**Root cause:**
- The solve's passes are either local (Gauss-Newton) or global in one surface at a time (each surface over its whole grid, the rest held).
- A move that only pays when two surfaces go together is never found. Example: one surface across its stall to a drag plate for yaw, with the surface beside it taking out the roll that makes.
- The allocator's own cost rates the drag pose better (bench), so the miss is in the search, not in the model.

**In the solver** (`Allocator.PairPassRun`, C#, dev key `AllocatorPairPass`, default **off**):
- A pair pass after the per-surface pass searches every pair of surfaces over both grids together: every other grid point, then the 8 points round the best.
- It uses the cost the solve minimises, scored over the servo transit as well as at the end.
- The native solve (the game's default) does not have it yet, so it stays off until the Rust port is done.

**Bench, managed solve, 60° limit, 9-case set:**

| | off | pair pass |
|---|---|---|
| Time to 20 deg/s velocity-vector roll (mean) | 2.61 s | 1.42 s |
| Roll at the limit, 300 / 420 / 550 km/h: time to 20 deg/s | 9+ / 4.45 / 2.23 s | 2.42 / 1.33 / 1.32 s |
| Max β, roll at the limit (300 / 420 / 550) | 4.2 / 4.1 / 3.2 | 3.7 / 3.6 / 5.3 |
| Max β, roll during the pull-up at 420 km/h (L / R) | 3.4 / 3.8 | 6.7 / 7.3 |
| Max β (mean) | 4.0 | 5.1 |

**Where the extra sideslip comes from** (roll during the pull-up, 420 km/h right):
- The pair pass puts the outer elevons into more body roll while the rudders are still in the conventional set and yaw is saturated (yaw shortfall ~90 deg/s²). So β builds to 7°.
- That is the cost's own roll / sideslip trade, now that the search reaches it.
- Turning off the sideslip-gated yaw weight (YawPrioHi) changes it only by 0.6°.
- Scoring over the transit changes it by 0.1°.

## 2. Short take-off: no automatic rotation (StovlGroundNoAutoRotate)

**Report:** in a short take-off the nose came up by itself at about 50 kt.

**Log 14:54, 39.25–40.25 s:**
- qCmd went 0 → 1.2 deg/s with the stick centred, as the CTOL blend came in (ctolW 0.31 → 0.83, 47–55 kt).
- The CTOL pitch law the blend runs from 40 kt is a 1 g / AoA hold. With the propulsion carrying up to 90 % of the weight, it pitched the nose up.
- The blend also walks the AoA to the STOVL reference (10°) once the legs are unloaded.
- The 5 % weight-on-wheels test reads "airborne" here, because the legs carry almost nothing.

**Fix:** in STOVL mode with the CTOL blend on and the stick centred or forward (≤ 0.05):
- While a wheel touches (0.2 % of leg travel), the blend's pitch command is zero. The CTOL pitch law is held at its start each tick, and the AoA walk is off.
- The hold stays latched from the roll until any of these: the pilot pulls (> 0.05), the jet is 10 m above the ground, or the speed falls below the blend (40 kt).
- So a jet that floats off the wheels on the propulsion keeps its attitude.
- A pull starts the law as on a mode change, and the pilot rotates.

**Bench**, full-lever short take-off from a standstill:

| | 0.3.6 | 0.3.7 |
|---|---|---|
| Stick centred: pitch attitude | rising from 37 kt; 1.9° at 58 kt, 8.9° by 160 kt, airborne | 0.2–0.4° up to 190 kt, still on the runway |
| Stick 0.4 at 10.3 s (~47 kt) | rotation as commanded | same, within 0.2° |

The VTO case differs from 0.3.6 by under 0.5° of pitch: its forward roll now meets the hold. Flight state in every other STOVL case is identical. The CTOL case set is identical to 0.3.6 (rebuilt release). `StovlGroundNoAutoRotate` false = 0.3.6.

## 3. Release guard timing

**Report:**
- After a block, letting go of the trigger and pressing again at once did not start the override. Tom had to wait a moment first.
- Wanted: hold 1 s, and the second store at 1.7 s.

**Cause:** the trigger counted as let go only after 0.15 s without a `WeaponManager.Fire` call. A quick re-press read as the same, still blocked press, so the override hold never started.

**Fix:**
- The game calls `WeaponManager.Fire` from `PilotPlayerState.PlayerControls` (Update) on every rendered frame the button is down (read from its code). So one frame without a call now counts as a release.
- `ReleaseGuardHold` 1.5 → 1 s. The 1.5 default is moved once; a hand-set value is kept.
- New `ReleaseGuardSecondHold` 1.7 s: with the trigger still held after an override, the next store comes off 1.7 s after the press. In 0.3.6 it came a fixed 0.7 s after the first, i.e. 2.2 s with the 1.5 s hold.

**Verification:** compiled only. The bench has no weapons path.

Benched, not flown.
