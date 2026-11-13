# F-22E FCS 2.12.1 — dead code out: the 2.6.9 two-axis predictor, the rudder-usage window gate, the rudder headroom sums

Base: 2.12.0. **Compiled + bench byte-identical (18 regression cases tick for tick on every remaining column; reversal
suite, corkscrew entries, utilisation invariant identical), not flown — nothing to fly, no behaviour change.** 2.12.0 kept
as `.dll.2.12.0.bak`. 9237 → 9091 lines.

## Deleted
1. **The 2.6.9 two-axis predictor** (`RateCapTwoAxis`, off since the 1 Nov findings — `claude/Governor_two_axis_predictor_findings.md`):
   `TwoAxisParams`, `AxisLag`, `AxisSwingCap`, `AxisSwingArea`, the rate-cap branch that used them, the table reversal
   acceleration chain feeding it (`rbIn`, `aLaw2`, `nAvRev2`, `capRud2`, `capR2`, `aT2`) and its seven bench-only telemetry
   columns (`twoAxisPred/Cap/Bind`, `tAxR/Y`, `aAxR/Y`). The statics `PaceHiFadeCap`, `PaceRudderFade`, `RateCapRudderValid`
   went with it (nothing else read them).
2. **The 2.5.x rudder-usage window gate** (`wRud` / `hiFade` from `stopSlipAbsF` / `yawUseAllF` on `PaceLoad0..1`, and the
   2.6.9 airframe-usage variant `PaceGateAirframe`) — computed every tick and overwritten by the measured gate (`gateZ`)
   since 2.7.0. `PaceHiFadeGate`, `PaceYawSatGate`, `PaceGateAirframe` statics gone.
3. **The rudder headroom sums** `nAvPos` / `nAvNeg` in the window's effector loop — only the deleted chain read them.

The dead table-based reversal "capacity" is therefore gone from the law entirely; the one table-derived device still
driving anything on the lateral side is `coupRollLim` (next).

## Still in, and why (unchanged list)
`coupRollLim` (table coupling cap, binding 8–14 % — replace on the estimator), the 41 % rate cap's reversal model (rebuild
with the two-axis *probe* through the allocator — not the deleted table predictor), the window's crossing latch (§4 b),
`DamageLatLimit` below the rudder fade and `FinLossAoACap` (after the no-fin high-AoA flight and the probe), `slipGov` + yaw
anchor (wing-damage gated, untested), `rollGov` (TVC pitch reserve — keeps its job), `PullRollTradeFinLoss` (inert at your
`PullRollTrade` 0.7; meaningful only above it).
