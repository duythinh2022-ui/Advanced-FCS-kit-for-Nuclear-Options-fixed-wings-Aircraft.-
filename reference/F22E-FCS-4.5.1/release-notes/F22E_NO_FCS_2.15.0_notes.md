# F-22E FCS 2.15.0 — the two-axis capacity is read on its limiting axis; the stab reserve yields where the stabs are the yaw

Base: 2.14.0. **Compiled + bench-checked (clean-jet regression set within ±0.3° except two cases 0.6° better; reversals,
corkscrew entries, utilisation invariant within noise), not flown. The no-fin case only exists in flight.** 2.14.0 kept as
`.dll.2.14.0.bak`. No tuning values touched; one new Dev-less static (`LatReservePitchFrac`, on) for the bench A/B.

## From the 19:54 flight (2.14.0, both fins off) — "so all of the no fins issues are not fixed"
Two things in the log, both in the law, neither a patch:

1. **The two-axis capacity over-read toward the shed side.** 2.14.0 probed along the rotation axis (cos α, 0, sin α) and
   *summed* the roll and yaw projections of what the probe returned. The flaperons return a large roll surplus, which the sum
   credited against the yaw shortfall: `capZe` toward the shed side read 25–73 deg/s² in the push-overs where the measured
   shed was 0.4–7. A shed cap fed 25–73 lets the pitch run as if the yaw could follow; it couldn't; +50 / +20° of sideslip.
2. **The stabs parked at the stop/reversal** (164.8 s): with the lateral stick in, `LateralStabReserve` 0.7 × `LatTrimGain` 100
   put the symmetric stab cost at 0.28 (base 0.0028), and the allocator left both stabs still for 0.5 s on 42–64 deg/s² of
   unmet yaw. The yaw only arrived once the roll target fell and both stabs moved *down together*. Without fins, at 65° AoA,
   symmetric stab motion *is* the yaw effector (20–28 deg/s² at 65°, ~5 near zero — the opposite profile to the rudders), so
   a reserve that keeps the stabs from moving symmetrically starves the very axis it is reserving for.

## Changes
**Estimator (`CapacityEstimator`):** the probe along u is now read as the *limiting-axis* composite: per group, each axis's
return is divided by its direction cosine and the smaller of the two is taken (`min(x/cos α, z/sin α)`), the window-distance
and committed terms likewise, and the unmet residual as the *larger* over the axes. A rotation about u can only go as fast as
its slower axis — the flaperons' roll surplus no longer pays for the yaw. `capZeP/N` = that composite × |sin α|, blended with
the rudder capacity by `rudW` as in 2.14.0. What it reads on the no-fin rotation is a flight number — the bench never makes
that rotation; the 19:54 push-overs (≤ 10 toward the shed side) and the 164.8 s stop (≈ 40) are the test.

**Stab reserve (`symCost`):** `symCost = StabTrimCost × (1 + LatTrimGain × latReserve × Lerp(stabPitchFrac, 1, rudW))`,
`rudW = clamp01(rudderAuth) × clamp01(yawHealth / 0.5)` — the same weight the estimator uses to decide whether the yaw
capacity is the rudders' or the stabs'. With the rudders present nothing changes. Without them (or with them faded at AoA),
the reserve goes with the pitch range the stabs still have: 0 at 65°, so the stabs are free to move together for yaw; full
at low AoA where the rudders' fade is gone and the reserve has a job.

The first cut relaxed the reserve by `stabPitchFrac` alone, fins or not. That moved the clean-jet bench both ways
(`fullaft+roll+rev3s 500` 5.0 → **8.2°**: the cheaper symmetric motion at the 65° entry changed the stab/rudder state the
reversal started from, and the allocator then took the left stab into its post-stall branch — p rose 27 → 39 against a −230
target for 0.4 s). Gating by `rudW` removes the clean-jet change and keeps the no-fin behaviour; the estimator fix alone moves
the clean bench ≤ 0.6° and in the right direction.

## Bench (sideslip ~2× high), 2.14.0 → 2.15.0
Regression set: push-then-pullthrough 450 7.4 → 6.8, fullaft+roll+rev3s 320 5.8 → 5.1, pull-then-pushover 450 7.2 → 7.3,
65AoA releases 6.6/6.7 → 6.8/7.0, the rest ±0.1. Reversals: fullaft roll rev 300 time-to-zero 1.45 → 1.23 s; leaf rev 160
2.92 → 1.12 s (beta 5.4 → 5.6); the rest ±0.1. Corkscrew entries ±1 deg/s. Utilisation invariant: all PASS, 98–99%.

## For the next log (both fins off ~200 kt, `FinLossAoACap` 65)
Leaf stops and reversals at 60–65°; released and held push-overs. Columns to read: `ceCapZeN/ZeP` (≈ 40 at the stop, ≤ 10 toward
the shed side in the push), `stabLlo/Lhi/Rlo/Rhi`, `stabAnti/stabSym` (the stabs should move together at the stop instead of
parking). And one clean-jet pass of reversals at 25–35° / 300 kt to confirm nothing moved there.

## Carried over
The removal list in the handoff §4 (`coupRollLim` on the estimator, the window latch, `DamageLatLimit` / `FinLossAoACap` after
this flight, `slipGov` + anchor, `PullRollTradeFinLoss`).
