# Revoker FCS 0.4.0

Base: **0.3.0**. Why your rates dropped, and the two inherent fixes for it. Minor bump: control-law additions (allocator + rate cap). 0.3.1 was never released; its contents are in here.

**Bench-only. Not flown.** Every number below is from the calibrated Revoker bench (`Revoker_bench_calibration_0.2.15.md`). The margin default moved (see §1) — read that before flying.

## What you reported, and what the logs said
Yaw and roll rate slower across the AoA range, push-overs slower, falling-leaf yaw 75 → 40–45. Your 0.3.0 log against the old stack's, by AoA band (|r| q99): 60–90° 77 → 54, 45–60° 61 → 29, 30–45° 53 → 32, 15–30° 46 → 35; sideslip q95 at 45–90° 7–8° → 4°. Two causes, both in 0.3.0, both fixed here — and neither was the margin:

## 1. The margin is now 8 (was 4), once
`RollStopSideslip` 4 → **8** with a once-only `GovernorRevision` migration: a player file still holding exactly 4 is moved to 8, a value you set by hand is kept. Your old stack ran this jet at 7–8° of sideslip above 45° AoA and the rates you were used to came with that; 4 is the Raptor's number, not this jet's. This alone bought little (bench corkscrew 34–51 → 44–55 deg/s) — it is the honest default, not the fix.

## 2. The allocator now weighs the lateral residual in the **velocity frame** (`SlipFrameAlloc`, on)
Of the two lateral errors the jet can make, only one makes sideslip: β̇ = p sin α − r cos α, so the error about the **velocity normal** (Δp sin α − Δr cos α) *is* the sideslip acceleration, and the error about the **velocity vector** (Δp cos α + Δr sin α) is stability-axis roll, which makes none. The allocator's roll weight is now the velocity-axis weight and its yaw weight the velocity-normal one. Below ~10° AoA sin α ≈ 0 and the two frames coincide, so nothing changes there; by 70° they have swapped, and the yaw priority follows the sideslip onto the body-roll axis. The body-frame high-AoA roll fade (`HighAoARollWeight`) is bypassed while the frame is on — it existed to compensate the very coupling the rotation removes. Measured AoA only: no schedule, no new constant. `false` = 2.6.6's body frame.

Why it matters, from the plant's own reversal (68° AoA, 300 km/h, full reversal; `printc`): at 6.6–7.2 s the roll command crosses zero, the yaw demand exceeds the nozzle at its stop, and the allocator buys yaw with the elevons — whose body-roll by-product at 70° is as large as their yaw. Roll asked −50 deg/s, **delivered +28** (wrong way; the canards are pinned by the pull). Body roll +13 while the yaw is already −20 → sideslip rate 21 deg/s → **8.6°** in 0.6 s. With the velocity frame that by-product is priced correctly: asked −25, delivered −21, and the same reversal makes **3.3°**.

Bench, this change alone (margin 8, kinematic cap): full-aft reversal at 320 **4.1 → 1.8°**; the 65° stick-release-with-rotation cases **5.4 / 5.7 → 1.8 / 1.8** (the 0.3.0 notes called that one "airframe/allocator, not governor" — this is that fix); steady rolls below 45° AoA **identical** (300 km/h, 16° AoA: 134 deg/s both; 42° AoA: 64 both), 70° AoA 61 → 54. Everything else in the regression set within 0.4°.

## 3. The rate cap is now a **forward model** of the reversal (`RevSim`, on)
The 2.5.8 cap estimated the sideslip of a full-stick reversal from a kinematic formula over a hand-picked yaw-effector set. Both halves were wrong here:
- The set (rudder + yaw nozzle) leaves out the trailing edge, which **is** the yaw axis at high AoA on this jet: per-surface on the calibrated plant at 350 km/h / 70° AoA the aileron pair makes **±68 deg/s² of yaw**, the rudder ±12, the canards ±4–10 (at 200 km/h: ailerons ±22, rudder ±4). The 0.3.0 reading of `eqerr yawsplit` ("the other effectors' yaw isn't real") was wrong: the plant's total matched your logs, so its aileron yaw is as real as the nozzle's.
- The formula assumes the yaw lags and the roll does not. At 70° both are slow.

Now, each tick: the trajectory swings the demand from +pS to −pS at its own reversal acceleration, the rate loops (`RollRateGain` / `YawRateGain`) and the sideslip hold ask for roll and yaw, a 3-axis least-squares allocation over **every** allocator effector delivers what it can (each from where it is, at its own slew, inside its allocator window, tables at the present flow, pitch held), and the body rates integrate with the probe's measured directional stiffness (yaw **and** roll) and yaw damping, with β̇ = p sin α − r cos α. Bisection (8 steps) for the largest rate whose reversal keeps the peak inside the margin. Statics `RevSimDt` 0.02, `RevSimTmax` 3, `RevSimMax` 300; telemetry `revSimBeta` / `revSimT`. Cost: **22 µs per tick** (0.1 % of a frame at 50 Hz), measured over 6000 ticks.

It only works *because* of §2: with the body-frame allocator the model predicted 1.9–2.5° where the plant made 8.3–8.6° (4× optimistic, so the cap would have been far too loose); with the velocity frame it predicts 0.8–5.5° against 1.8–3.3° actual — truthful to about a factor of 2 in both directions, which the margin absorbs.

## Bench, 0.2.22 (old stack) → 0.3.0 (what you flew) → 0.4.0
| | stops worst / mean | reversals worst / mean | sweep mean roll | 65–70° corkscrew pS | corkscrew β |
|---|---|---|---|---|---|
| 0.2.22 | 4.2 / 2.57 | 7.9 / 3.91 | 93 | 72–81 | 1.7–3.3 |
| 0.3.0 | 2.9 / 1.92 | 4.6 / 2.33 | 84 | 34–51 | 1.0–3.3 |
| **0.4.0** | **3.7 / 2.26** | **4.9 / 2.60** | **99** | **62–71** | **1.1–2.5** |

Falling leaf, full aft + full lateral at 70° AoA (velocity-vector roll rate / peak sideslip): 0.2.22 **71 / 11.3°**, 0.3.0-style (margin 8, body frame, kinematic cap) 57 / 6.5°, **0.4.0 82 / 2.5°** at 110 km/h and 76 / 1.4–2.0° at 200–300. More rate than the old stack at a quarter of its sideslip.

Regression set, 0.3.0 (margin 8) → 0.4.0 — unchanged within 0.4° except: full-aft reversal 320 4.1 → 1.8, 65° releases 5.4/5.7 → 1.8/1.8, sweep stops 3.0 → 3.7 worst (the governor using the margin it is now given), mean sweep roll 88 → 99.

## The knobs, if the feel is still wrong
In order: `RollStopSideslip` (8; lower = cleaner stops, slower rolls), `RollPaceRateBlend` (0.4; 0 = window only, 1 = cap only), `RollPaceRateSlip` (1). New A/B statics (Dev, `Name=value` on the bench): `SlipFrameAlloc` false = the old body-frame allocator, `RevSim` false = the old kinematic cap. Those two are the whole of 0.4.0's behaviour change.

## Not done / known
- **Nothing flown.** The bench is the calibrated one, but it is a bench.
- The Raptor does **not** get §2: on its own bench the velocity frame makes its full-aft reversals worse (4.8 → 9.2°, 6.4 → 8.3°) — its weak axis at high AoA is roll, not yaw, so the heavy weight lands on an axis it cannot move and the rudders under-deliver. That needs an authority-aware weight before it can go on the Raptor; the Raptor stays 2.6.6.
- The nozzle's yaw table reads ~1.5× what the plant delivered at 50 m/s (thrust model at low speed) — separate item.
- Your 00:21 log: 10.0–10.5 g held for a 9.5 g command at 500 m/s IAS (12.1 with full roll + rudder). Real G-loop overshoot at high q, not touched here.
- Your Revoker "disintegration" was the game's joint model with the airbrake set out at q ≈ 160 kPa — you reproduced it on the stock jet. Not an FCS item.

## Install
- **Main:** `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 592c4d4b581974e342aa384c1e44bd17) and `meta.json`, version 0.4.0. Dev DLL unchanged.
- **Backup:** 0.3.0 kept as `…\Revoker_FS12_FCS.dll.0.3.0.bak` (0.2.22 is still there as `.dll.0.2.22.bak` — the old lateral stack).
- **Release:** `C:\Games\NO mods\Revoker-FCS-0.4.0-release`. The source zip carries the bench with `rsync.sh`, `tomrev.cfg`, `ifly … fcslog=<csv>`, `bench ieff` (per-surface plant authority, now using this jet's surface names), and `an/` (`ssweep.py`, `sreg.py`, `cork.py`, `runone.sh`, `rev65.sh`).

## First flight — what to look at
1. **Falling leaf and 65–70° rolls:** the rate. Bench says 76–82 deg/s against the old stack's 71. If it is still slow, `RollPaceRateBlend` 0 first (window only), then the margin.
2. **Reversals at 10–25° AoA:** sideslip and crossing time; `revSimBeta` in the log is the model's own prediction — if it reads far under the sideslip you actually make, tell me, that is the model being optimistic again.
3. **The −55° push + roll entries at 90–110 kt:** should be at least as quick as 0.3.0.
4. `rollGate`, `yawCap`, `slipDamp` and `dampCredit` are the other new columns.
