# F-22E FCS 2.8.0 — the pitch side on the measured capacity (trade, AoA-rise cap, shed cap)

Base: 2.7.6. **Compiled + bench-checked, not flown.** Minor bump: a control-law change. 2.7.6 kept as `.dll.2.7.6.bak`.
`TradeMeasured = false` (Dev) with `SweepShedCos = 0.5` is exactly 2.7.6 (bench: identical).

## The re-pull hitch (15:02 log, 210.3 / 215.0 / 224.1 s) — cause, then fix
At 265 kt / 3° AoA, full roll (p 177 deg/s), roll released and full pull together. `qCmd` 50 → 30 → 60 within 0.5 s while
the roll is already at 42 → 0 and the yaw idle (`unmetZ` 0, `yawSat` 0, sideslip 2°). The trade's pitch share
(`tradeAlpha` 24–30) was computed from the **roll limit** (`pSmax` 125 → 82), not the roll that exists: pitch =
ask × (roll needed ÷ roll limit), held until the slewed limit climbed back (`PullTradeRise` 120 deg/s², 82 → 125 ≈ 0.35 s).
A fixed 0.7 / 0.3 split applied to a roll that was not there.

**Fix (inside the trade, `TradeMeasured`):** the yaw a rolling sweep needs is pS·cos α·α̇. It is computed against the roll the
**stick asks** (`pSAskStick`, before any cap) and the **measured capacity** on the side the sweep needs
(`CapacityEstimator`, last tick), `TradeCapShare` 0.7 of it. No shortfall → no trade, the pitch runs free whatever the roll
limit is doing. A shortfall is split `PullRollTrade` (0.7) to the roll target and the rest to the AoA rate — your trade,
unchanged in meaning, on the right quantities. A released stick asks nothing of the pitch.
Bench, the same manoeuvre (490 km/h, full roll 1.5 s, release + full pull): old `qCmd` 50 → 30 → 60 with q stalled at 35 for
0.3 s; new `qCmd` 14 → 60 monotonic, q 2 → 59 smooth, sideslip 1.8° both.

## The AoA-rise cap and the sweep cap read the estimator
`capNeed` / `aYawSw` (the pitch-side caps in `LateralLaw`) read `capEst.capZP/ZN` instead of the 2.7.x figure (which read
1.8× the realised yaw in reversals; the estimator 1.2–1.4× in the 12:31 flight).

## The push-over shed cap: continuous, from the estimator
`aYawShed` = the estimator's capacity on the side that sheds the rotation; `SweepShedCos` 0.5 → 0.05 so the cap
(SweepShedK × |AoA| × capacity ÷ yaw excess) stands at every AoA instead of easing off below ~40° — the 12:31 log's 20.8°
came from the cut letting go at 18° AoA with 36 deg/s of yaw still on, then the fins stalling past 10° of sideslip. Smooth over
fast (your 2 Nov call). Bench (300 km/h, released-roll push from 65°, r 40 at release): sideslip 7.0 vs 7.3, time to zero
AoA 1.72 vs 1.67 s — the bench's leaf carries half the game's yaw rate, so the game case is the test.

## Regression set (bench, sideslip ~2× high), 2.8.0 vs 2.7.6
Better: push+roll 300 3.6 vs 7.1; fullaft+roll+rev3s 6.2 / 6.1 vs 7.7 / 6.6. Same within 0.6°: 11 cases. **Worse:**
roll-then-pull-rev 400 **8.7 vs 5.3** (the pull is no longer held, so the reversal comes at 31° instead of 28° with the roll at
118 instead of 112), pull-then-pushover+roll 450 6.4 vs 3.8, pullthrough with roll released at zero 350 6.2 vs 4.1. Rolls are
faster everywhere (pMax +3 to +15 deg/s) because the trade stops taxing them pre-emptively. Those three are the roll-side rate
cap's job (still the 2.7.x figure) — the next item.

## Also found in the 15:02 log: the corkscrew roll-rate dip is two caps releasing at the rudder fade
400 kt entry: the measured velocity-vector roll 100 deg/s at 20° → 37 at 56–59° → 64 at 64°. The binding cap through 30–60° is
`coupRollLim` (123 → 51), which climbs to 74 past 60°. 275 kt entry: 65 → 34 at 55° → 62 at 64°; there the binder is the rate
cap's 41 % blend (`paceHold` −20 → −12 → 0), which fades with `rudderAuth` (1 → 0 over 50–60° AoA, by the 2.6.9 design "so
the falling leaf is not taxed") and so lets go exactly at the top. Both are the roll side: §4 (a) and item 3 of the handoff.

## What to fly
The re-pull after a fast roll (the 210 s manoeuvre); released-roll push-overs from 65°; reversals from a held pull at
250–400 kt (the regression set's worse cases); corkscrew entries (unchanged by this build).
