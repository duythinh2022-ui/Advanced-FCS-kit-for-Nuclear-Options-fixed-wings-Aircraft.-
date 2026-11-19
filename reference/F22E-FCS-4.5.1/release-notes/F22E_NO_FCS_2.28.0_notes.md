# F-22E FCS 2.28.0 — the single reference-capacity governor: the push-over special cases removed

Base: 2.27.0. **Compiled + bench-checked (full suite) + replayed from the logged states of the 14:45, 14:23, 13:59 and 13:06
pushes, not flown.** No player/Dev keys; CSV 306 columns (`sweepDel` column is now `capSm`, the smoothed capacity). 234 lines
shorter than 2.27.0.

## What Tom said (14:51, after flying 2.27.0)
"Released push-over is smoother now. Held push-over is still a little slow and a little hitchy. It should be a single
reference-capacity governor: it limits the lateral rate when only the yaw is the bottleneck and limits the pitch rate when both
yaw and roll are the bottleneck." And before that: the rule is no on-top fixes. 2.24.0–2.27.0 had stacked caps, filters, a slew, a
floor and a latch with five gates on the push-over. All of that is gone; three general statements replace it.

## The law now (`LateralLaw`, one sweep block; the trade in the pitch law)
1. **One AoA-sweep pace, both directions, held or released — the trade.** The yaw a rotation needs changes with the AoA at
   pS cos α × AoA-rate. Against the yaw capacity on the sweep's side (one read, smoothed once in the estimator over `SmoothCapTau`
   0.2 s — `capSm`; a capacity cannot physically change faster than the surfaces move) a shortfall is split as the 2.8.0 trade has
   always split it: `PullRollTrade` 0.7 of it comes off the **roll target** (the lateral rate is limited first), the rest off the AoA
   rate. 2.6.5 had excluded the sweep toward zero from the trade; 2.2.25 let it cut only a roll that was still building. Now it acts
   on both sweeps and cuts an established roll on the held sweep toward zero — the cut paced at the yaw's rate (the trajectory may
   not brake the velocity roll faster than capacity / sin α), so it is not the sideslip swing 2.2.25 feared. The pitch is also bounded
   by the rotation the jet **has** (`TradeCapShare` × cap / (pS cos α), 2.5.4's rule): while the roll cannot come down — the roll
   axis saturated braking a rotation the airframe keeps up by itself — the pitch gives. That is "limit the pitch when both are the
   bottleneck", as a consequence, not a case. With the stick released there is no roll target to trade, the yaw sheds the whole
   rotation and the pace is the one that keeps the body roll from growing while it does: |AoA-rate| ≤ `ReleaseFollowPaceK` · a · sin α
   cos α / r. Blended by the released weight. The sweep is never paced below the flight-path rotation (the nose is never turned
   back) — a statement of the pace, held or released, not a clamp.
2. **A released stop follows the slower axis.** With the lateral stick released (inside `SweepNoReverseStick` 0.2 of centre and
   having stayed there `StickReleaseDwell` 0.12 s — a reversal's stick crosses the centre in a few ticks and is not a release), the
   roll target is the coordinated roll for the yaw the jet has (r / sin α), never more than the body roll it had when the stick came
   off, weighted by how much longer the yaw needs than the roll: w = clamp01(t_yaw / t_roll − 1), t_yaw = |r| / yaw capacity, t_roll =
   |p| / the trajectory's tuned brake. At low AoA w = 0 and the stop is the crisp roll-limited stop; at 65° with a rotation on it is 1.
   The yaw loop is asked for its brake capacity directly (2.20.0's rule, general). No fin-health term: with a fin gone the yaw
   capacity is smaller, t_yaw longer, w larger — the law already knows.
3. **The capacity is read once.** `capSmZeP/N` in `CapacityEstimator`; the trade on the sweep that grows the demand keeps the 3-tick
   read (`capTrZe`, 2.21.0: a lag there is a real loss missed).

## Deleted (in the source zip's 2.27.0 for the record)
Kinematic sweep cap (`SweepYawK`, `SweepPerAxis`, `SweepRollShare`), shed cap (`SweepShedK`, `SweepShedMeasFloor`, `SweepShedNoStab`,
`SweepShedCos`, `SweepShedSlip`), delivered term (`SweepDelivered`, `SweepDelTau`), crossing lead (`SweepCrossLead`), cap filters and
slew (`SweepCapTau`, `SweepCapSlewDn/Up`), the floor's held/released switch (`SweepNoReverseHeld`), the follow latch and its gates
(`ReleaseFollowAoA0/1`, `ReleaseFollowStick`, `ReleaseFollowDwell`, `ReleaseFollowPushStick`, `ReleaseDropSweepRel`,
`ReleaseFollowCapPS0`, `ReleaseFollowFinPow`, `ReleaseFollowDropSweep`), the rudder window (`RudderWindowSweep` + 3), the 2.18.0
no-fin `ReleaseCoordinate` (statement 2 with w = 1). `PushoverSideslip` (player key) is idle: the yaw it would leave to the rudders at
the bottom is yaw the followed roll would have to carry at r cot α (bench: body roll 90 deg/s at 10° AoA, bottom sideslip doubled).

## Bench (final suite vs 2.27.0; `suite_v228_vs_v227_*.diff`)
- Reversals **identical**, corkscrew entries ±0.1, utilisation identical (4 FAIL / 9 as since 2.9.0).
- Regression set: 65° releases 4.2 → 2.8 / 2.9, push-then-pullthrough 450 8.3 → 6.2, pullthrough-released 350 4.4 → 6.2, rest ±0.5.
- Damage suite: **held clean pushes unchanged** (7.5 / 8.5, after-push 4.5 / 4.1); clean released pushes 7.5 → 10.2, 8.5 → 10.5 on the
  2× bench — its leaf sheds half the game's yaw, so the honest pace (read 45–60 at the top where the bench delivers 20–30) runs ahead of
  its yaw there; **on the game's own states** (14:45 log) the new pace gives −12, −5, −5, −6, −8, −7, −5, −7, −10, −15, −20 where
  2.27.0 flew −34, −28, −11, −2, 0, 0, −1, −4, −8, −16 (the plateau gone), and on the 203 kt push it is never tighter than what flew.
  No-fin pushes 18.0 → 20.2 / 13.0 → 13.9 (the 2.25.0 filter's gain, back to 2.24.0's figures); one-fin released 2.5 → 7.1 / 4.8 → 9.3
  (the law follows the slower axis with one fin as it should; the 2.26.0 fin-health exponent that halved this was a gate); one-fin held
  9.3 → 8.9; leaf stops 0.3–1.3 better; LERX-off hold 3.2 → 1.2.
- Replays of the 14:45 held pushes (bench): 109.2 s — command −21, −39, −40, −40, −32, −24, −19, −15, −10, −5, −2, −3, −8, −14, −21 …
  −33 (2.27.0 flew −25 … −30, −16, −9, −4, −1, 0, −1, −4, −9 … — the stop is now an ease; the roll target cut 83 → 37 while the body roll
  stays 60 because the airframe holds it: both axes the bottleneck, the pitch gives); 154.8 s −29, −47, −50 … −45, −42, −39, −34, −25,
  −15, −5, −3, −9, −16, −21 …; 125.4 s (135 kt) −25, −42, −38, −30, −25 … −6, −5, −6, −9 … −24, sideslip ≤ 4 (2×).
- Released replays as 2.27.0 within the bench's yaw (13:06 63.1 s: body rate 21, 15, 7, −1, −9, −15, −21, −26, −30, −32, −33 … −56, one
  curve).

## What to expect
Released push: as 2.27.0 or smoother (no first-tick overshoot: the pace is in force before the stick moves). Held push: the roll
target comes down first (you will see the velocity roll slow as the yaw saturates), the nose follows one curve; where the airframe
keeps the body roll up on its own through 45–30° the pitch eases — a slowdown, not a stop. `pSTarget` shows the trade's cut, `capDn`
the pace, `capSm` the capacity it divides by, `relFollow` the released weight. `LogOutput.log` must say 2.28.0.
