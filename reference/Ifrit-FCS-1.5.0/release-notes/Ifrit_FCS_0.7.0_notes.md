# Ifrit FCS 0.7.0

Base: **0.6.0**. The same two inherent fixes as Revoker 0.4.0, for the same reason: the rates you lost in 0.6.0. Minor bump: control-law additions (allocator + rate cap). 0.6.1 was never released; its contents are in here.

**Bench-only. Not flown.** The Ifrit bench is still thin (`Ifrit_bench_calibration_0.5.6.md` — one 33 s log plus a 20 s held-out), so treat its sideslip numbers as directional. The margin default moved (§1).

## 1. The margin is now 8 (was 4), once
`RollStopSideslip` 4 → **8** with a once-only `GovernorRevision` migration (exactly 4 → 8; a hand-set value is kept). Your old stack flew this jet with `PullSideslipMargin` **15** and made 5–9° of sideslip in high-AoA rolls on the bench; 4 is the Raptor's number. On its own this changed the 65° corkscrew rate not at all (31–34 deg/s before and after) — the cap was binding elsewhere.

## 2. The allocator weighs the lateral residual in the **velocity frame** (`SlipFrameAlloc`, on)
β̇ = p sin α − r cos α, so the error about the velocity normal (Δp sin α − Δr cos α) *is* the sideslip acceleration and the error about the velocity vector (Δp cos α + Δr sin α) is stability-axis roll, which makes none. The roll weight becomes the velocity-axis weight, the yaw weight the velocity-normal one; the body-frame high-AoA roll fade is bypassed while it is on. Identical below ~10° AoA (the frames coincide), fully swapped by 70°. Measured AoA only — no schedule, no constant. `false` = 2.6.6's body frame. Full derivation and the Revoker's plant trace that found it: `Revoker_FCS_0.4.0_notes.md` §2.

Bench, this change alone (margin 8, kinematic cap) — the cases you complained about first:
- **−55° push + roll corkscrew entry at 170 km/h: 5.1 → 2.6°**; roll-then-push −55 at 200: **4.2 → 3.0°**; push + roll at 200: **4.7 → 3.1°**.
- 65° stick-release with a rotation on: 5.2 / 4.9 → 4.9 / 4.4. Sweep stops worst 4.7 → 4.0.
- Costs: pull-then-pushover+roll 350 3.0 → 4.0°, push-then-pullthrough 450 8.6 → 8.9°, pull-then-roll 250 roll 26 → 21 deg/s. Steady rolls below 45° AoA unchanged.

## 3. The rate cap is a **forward model** of the reversal (`RevSim`, on)
Replaces the kinematic formula over a hand-picked yaw-effector set. On this jet the set was the canted tails' differential plus the split decelerons — and the per-surface plant dump says the trailing edge is the yaw axis at high AoA: at 150 km/h / 65° AoA the **aileron pair makes ±12.5 deg/s² of yaw, the flaps ±4.8, the tails ±2.2**, the splits ±2 (at 250 km/h: ailerons ±35, tails ±6). With the tails alone the cap sat on its 10 deg/s floor from 50° AoA up, and `RollPaceRateBlend` 0.4 passed 60 % of the stick — that is where your 40 deg/s came from.

The model: trajectory reversal at its own acceleration, rate loops and the sideslip hold asking, a 3-axis least-squares allocation over every allocator effector (from where it is, at its slew, inside its window, tables at the present flow, pitch held), the probe's measured directional stiffness (yaw and roll) and yaw damping, β̇ = p sin α − r cos α, bisected for the largest rate whose reversal stays inside the margin. `RevSimDt` 0.02, `RevSimTmax` 3, `RevSimMax` 300; telemetry `revSimBeta` / `revSimT`. 22 µs per tick (0.1 % of a frame at 50 Hz). It is usable only because of §2 — with the body-frame allocator the model was 4× optimistic.

## Bench, 0.5.6 (old stack) → 0.6.0 (what you flew) → 0.7.0
| | stops worst / mean | reversals worst / mean | sweep mean roll | 65° corkscrew pS | corkscrew β |
|---|---|---|---|---|---|
| 0.5.6 | 5.5 / 3.39 | 7.2 / 3.73 | 109 | 45–57 | 1.2–7.5 |
| 0.6.0 | 4.7 / 2.38 | 7.2 / 3.24 | 85 | 31–34 | 0.7–4.5 |
| **0.7.0** | **6.0 / 3.61** | **6.0 / 3.94** | **101** | **39–43** | **1.0–4.9** |

Falling leaf, full aft + full lateral at 65° AoA (roll rate / peak sideslip): 0.5.6 40 / 5.3° at 110 km/h and 45 / 2.8° at 300; 0.6.0-style 26 / 4.9° and 32 / 3.6°; **0.7.0 29 / 5.1° and 42 / 2.5°**. Most of the rate back at the old stack's sideslip. Note the stops: 4.7 → 6.0 worst is the governor spending the 8° margin you now give it (the old stack made 5.5 with no margin discipline at all). If that feels loose, the margin is the knob.

**The Ifrit stays slower than the Revoker at 65° by its own authority**, not by the governor: at 150 km/h the whole airframe makes ~12 deg/s² of yaw there against the Revoker's nozzle-led 75.

## The knobs
`RollStopSideslip` (8), `RollPaceRateBlend` (0.4), `RollPaceRateSlip` (1), then `PaceStiffCredit` / `SweepMarginGate` / `PaceDampCredit` as in 0.6.0. A/B statics: `SlipFrameAlloc` false = old allocator, `RevSim` false = old kinematic cap.

## Not done / known
- **Nothing flown**, and the bench is thin — two or three more varied Ifrit logs on the current build are still the ask before leaning on its sideslip numbers.
- The Raptor does not get §2 (it regresses there — see the Revoker notes); the Raptor stays 2.6.6.
- `pull-then-pushover+roll 350` and `push-then-pullthrough+roll 450` are the two cases that got slightly worse; both are inside the margin.

## Install
- **Main:** `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 40df78dd8a9cc522a637b1cecdd03a7e) and `meta.json`, version 0.7.0. Dev DLL unchanged.
- **Backup:** 0.6.0 kept as `…\Ifrit_KR67_FCS.dll.0.6.0.bak` (0.5.6 is still there as `.dll.0.5.6.bak`).
- **Release:** `C:\Games\NO mods\Ifrit-FCS-0.7.0-release`. The source zip carries the bench with `isync.sh`, `tomifr.cfg`, `ifly … fcslog=<csv>`, `bench ieff`, and the `an/` scripts. The NOMM listing in the plugin folder is not updated — your release step.

## First flight — what to look at
1. **The −55° push + roll entries at 90–110 kt** — the case you flagged. Bench says 2.6–3.0° against 0.6.0's 4.2–5.1 and a quicker entry.
2. **Falling leaf / 65° rolls:** the rate (bench 29–42 deg/s vs 0.6.0's 26–32, old stack 40–45).
3. **Reversals at 10–25° AoA:** sideslip. `revSimBeta` is the model's own prediction — if it reads far under what you make, the model is optimistic and I want the log.
4. If the splits open in ordinary reversals (`splitL/R`), tell me — that is drag you did not ask for.
