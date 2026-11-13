# F-22E FCS 2.7.5 — measured capacity estimator, telemetry only

Base: 2.7.4. **Compiled, bench-checked, not flown.** No control-law change: the bench regression set (18 cases) is
byte-identical with the estimator on and off, every non-estimator column. 2.7.4 kept as `.dll.2.7.4.bak`.

## What it adds
`CapacityEstimator.cs`, called once per tick after the allocation, logging 44 `ce*` columns. Per axis (yaw Z, roll X)
and side (P/N), the capacity toward that side in deg/s^2 is composed of three parts that are each measured or the
allocator's own decision — no independent table sums:
1. `ceWdZ/X`: the filtered measured angular acceleration (everything acting now, passive and active);
2. `ceCm{Z,X}{r,s,f}`: the committed increment still arriving — table moment from `now` to this tick's command, per
   effector group (rudders / stabilators / flaperons), times the group's gain;
3. `cePr{Z,X}{P,N}{r,s,f}`: an allocator probe — the allocator solved once more from this tick's commands for an extra
   ±400 deg/s² of yaw (±1500 of roll) with the rest of its target unchanged, read per group, commands restored.
   `cePr*x/z`: the probe's collateral on the other axis.
Gains `ceG{Z,X}{r,s,f}` are identified online (RLS on 0.1 s differences, prior 1, forgetting 10 s, excitation-gated,
clamped 0.2–1.6; `ceGUpd*` = updated this tick). `ceCap{ZP,ZN,XP,XN}` is the composite with the online gains,
`ceCap*raw` the same with every gain at 1. Statics for the bench / Dev DLL: `CapEstOn`, `ProbeZ`, `ProbeX`,
`ProbeIters`, `GainTau`, `GainRidge`, `GainLo`, `GainHi`, `GainExcite`, `DiffTicks`.

## Offline validation on the six 2 Nov logs (bench mode `fcsmom`, same composition with fixed gains 1.0 / 0.7 / 1.0)
Per-group gains from 62 701 samples: yaw rudders 0.93–1.05, stabilators 0.64–0.77, flaperons 0.8–1.0 at 20–60° AoA;
roll rudders ~1.0, stabilators 0.56–0.72, flaperons 0.9–1.2. Nearly constant with AoA — the tables are not 3–4× off at
high AoA at the deflections in use; what overstates is the *independent headroom sum* (every effector to its stop) that
the allocator never delivers when the same surfaces carry roll and pitch.

Estimate / realised peak yaw acceleration in the 0.6 s after the event (median [10–90 %]):
| regime | this estimator | raw probe (gains 1) | 2.7.x `capZ` | independent table headroom |
|---|---|---|---|---|
| reversals < 40° AoA needing > 30 deg/s² (n=35) | 1.45 [1.15–2.70] | 1.10 [0.49–2.49] | 1.82 [1.03–3.33] | 2.20 [1.67–3.84] |
| reversals ≥ 40° (n=5) | 1.48 [1.14–1.87] | 0.84 | 1.51 | 1.95 |
| push-overs from 60°+ (n=18) | 1.14 [0.88–1.65] | 0.46 | 0.97 | 1.37 |
| yaw-limited (`unmetZ` > 10 or `yawSat` > 0.2, n=15) | 1.08 [0.93–1.42] | 0.43 | 0.92 [0.76–1.14] | 1.29 |
The upper tail of the reversal row is demand-limited (reversals at 1–9° AoA need little yaw). Never below 0.88× where
the yaw was the limit — the 2.7.x figure needed the 2.5.12 ceiling hack to get there.

## Cost
Four extra allocator solves per tick: ~0.35 ms on the build container (bench, 481 ticks: 1.45 vs 1.26 s). Set
`CapEstOn = false` in the Dev DLL to remove it.

## What to fly
One ordinary flight with everything: reversals at 15–45° from a held pull at 250–450 kt, corkscrew entries at 300–400 kt,
falling-leaf reversals, released- and held-roll push-overs from 65°. The log's `ceCapZP/ZN` against the yaw
acceleration the jet then makes per event, and `ceGZ*` against 1.0 / 0.7 / 1.0, decide whether the online gains can
replace fixed ones and whether the estimator drives the new governor.
