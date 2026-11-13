# F-22E FCS 2.14.0 — the two-axis probe: capacity along the velocity-vector roll

Base: 2.13.0. **Compiled + bench-checked (fins present: regression set within ±0.6°, reversals / entries within ±0.2°), not
flown — the case it is built for (no fins, and the clean jet past the rudder fade) only exists in flight.** 2.13.0 kept as
`.dll.2.13.0.bak`. Estimator change + every consumer; no tuning values touched.

## Why (18:47, 19:30 logs, both fins gone)
Past the rudder fade — and without fins at any AoA — the yaw the governor budgets is not an axis of its own. The velocity-
vector roll is one demand split cos α : sin α across body roll and yaw, served by the same surfaces. The estimator probed
yaw alone, with the roll target held, and the allocator answered by re-purposing the stabs and flaperons for yaw that the real
allocation keeps on roll and pitch: 24–49 deg/s² claimed toward the shed side in the no-fin push-overs against 10–25 made
(33.6°, 57.8°). At the no-fin stop (19:30 log, 164 s) it went the other way: the allocator's per-axis unmet counted one
shortfall on both axes (roll −26, yaw −64 for the same rotation), the 2.8.1 "probe − unmet" wiped the probe out, and the
capacity read 8–13 where a replay of that state through the effector tables gives the stabs 40 toward the brake. The window
held the target on that number for 1.7 s — Tom: "very, very hesitant".

## Change
`CapacityEstimator`: a third probe along u = (cos α, 0, sin α) — the allocator solved from this tick's commands for
`ProbeS` 800 deg/s² more velocity-roll acceleration each way, the rest of the target unchanged; each group's delivery read as
its (roll, yaw) acceleration increment projected on u with the per-axis gains; the measured acceleration, the committed
increment and the allocator's unmet each projected on u once. `capSP` / `capSN` = capacity along u (deg/s² of velocity-roll
acceleration). The figure the consumers read, `capZeP` / `capZeN`, is the yaw component of that (capS × |sin α|) where the
rudders are not the yaw axis, blended to the yaw-only figure where they are: rudW = rudderAuth × min(1, yawHealth / 0.5)
(1 below 50° AoA with at least one fin; 0 above 60° or with both fins gone). Consumers switched: the trade's budget, the
AoA-rise cap, the sweep cap, the shed cap, the rate cap's reversal capacity.

With fins, above the fade, the two figures agree on the bench (65° release cases: capZ 24–28, capZe 24–28, capS 26–30) —
the sanity check. Without fins the expectation from the logs: the no-fin stop reads the stabs' ~40 instead of 8–13 and the
window lets the reversal through; the no-fin push reads ≤ 10 toward the shed side instead of 24–49 and the pace holds the
nose up. Both are flight questions.

Telemetry: `ceCapSP ceCapSN ceCapZeP ceCapZeN ceRudW cePrSPs cePrSNs cePrSPf cePrSNf`, and for the stab diagnosis
`stabLlo stabLhi stabRlo stabRhi stabAnti stabSym` (the stabilators' allocator windows and pair costs — the next no-fin stop
will show whether something is holding them).

## Bench (fins present, sideslip ~2× high)
Regression set: 15 of 18 within ±0.2°, push-then-pullthrough 450 6.8 → 7.4, pullthrough released 7.5 → 7.6, pull-then-
pushover 350 4.4 → 4.3. Reversal suite and corkscrew entries within ±0.2°, utilisation invariant unchanged. The small
drifts below the fade, where capZe = capZ exactly, are the two extra allocator solves per tick perturbing its warm start.
