# F-22E FCS 2.7.6 — capacity estimator finalized from the 12:31 flight (telemetry only)

Base: 2.7.5 (= 2.7.4 in behaviour). **Compiled, bench-checked, not flown.** Bench regression set (18 cases) byte-identical
with the estimator on / off and against 2.7.4, every non-estimator column. 2.7.5 kept as `.dll.2.7.5.bak`.

## What the 12:31 log (2.7.5, 267 s) said about the estimator
- Logged `ceCapZ` vs the realised peak yaw acceleration, events needing > 30 deg/s²: reversals 1.4 / 1.4 / 1.4 / **0.2**,
  push-overs from 65° 1.2 / 1.2, yaw-limited events 1.2–1.4. The 0.2 (256.8 s, 250 kt, 30° AoA) is the online per-group
  gains: all three yaw gains sat at the 0.2 clamp 22 % of the flight (roll 58–68 %) — stabs and flaperons move together, the
  three-parameter RLS is not identifiable in flight. With the fixed gains the same event reads 1.2.
- Push from 65° at 69.6 s (the 20.8° sideslip): the committed stab increment (a 65° differential swing, 0.65 s of servo
  travel) was counted whole and the allocator reversed it 0.5 s later for the push; the estimate read 56–62 against 7–26
  realised at 65→50° AoA. Counting only the travel that arrives within 0.15 s brings the composite to 0.7–1.2× there and
  leaves the mid-AoA reversals at 0.9–1.5×.

## Changes (all `CapacityEstimator.cs`, all telemetry)
- Per-group gains are statics from the 2 Nov logs: `CapGainZR/ZS/ZF` 1.0 / 0.7 / 1.0, `CapGainXR/XS/XF` 1.0 / 0.65 / 1.0.
- The online identification is one lumped gain per axis (`ceGZ`, `ceGX`; achieved per modelled total effector moment),
  logged, not used.
- The committed term is discounted per effector to `min(1, rate × CapHorizon / |cmd − now|)` (`CapHorizon` 0.15 s);
  `ceDr/Ds/Df` log each group's arriving share. `ceCap*` = fixed gains + discount; `ceCap*raw` = gains 1, undiscounted.
- Columns: `ceGZr…ceGXf` replaced by `ceGZ,ceGX,ceDr,ceDs,ceDf` (42 `ce*` columns).

## The 20.8° sideslip at 71.7 s, dissected (2.7.4 behaviour)
Corkscrew at 65° / 230 kt, r −72 deg/s (the velocity-vector roll is yaw up there); roll stick released at 69.4, full push
at 69.6. The yaw had to be shed before the AoA reached ~10°. What it got: 7–26 deg/s² from 65° to 50° (rudders faded,
stabs on the roll stop and the push, TVC at −20), then 35–55 with the rudders at the stops. 2.7.4's pitch cut
(`yawSatPitch` −30…−45, `qCmd` −14…−16) held the sweep to ~32 deg/s down to 18° AoA, then released to −60 at 71.0–71.1 with
r still −36: the last 20° of AoA took 0.5 s and β̇ ≈ −r·cos α ≈ 36 deg/s put 18° on. From 10° of sideslip the into-wind
rudder is stalled (`Rudder_R_aoaNow` 51–55°) and the allocator holds the other at the table's peak (−34…−37° local,
deflection retreating 35 → 14° as β grows) with `unmetZ` 50–65: the capacity collapses exactly when needed. This is the §4
(c) accounting of the handoff — the pitch rate must be a continuous function of the yaw rate still to shed and the capacity
to shed it, never a cut that lets go — and the reason the sweep must stay inside the margin: past ~10° the fins are gone.

## What to fly
Same list as 2.7.5; one flight. `cap_val_flight.py <log>` gives the per-event table.
