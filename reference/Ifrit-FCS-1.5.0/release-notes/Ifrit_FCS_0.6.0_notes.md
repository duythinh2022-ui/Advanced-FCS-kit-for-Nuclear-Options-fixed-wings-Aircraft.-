# Ifrit FCS 0.6.0

Base: **0.5.6**. The F-22E's reference governor (2.6.6) ported, the rolling-pull governor and the rest of the old lateral sideslip stack retired as F-22E 2.6.0 did. Minor bump.

**Bench-only. Not flown.** The calibrated Ifrit bench (`Ifrit_bench_calibration_0.5.6.md`) is thin — one 33 s log and a 20 s held-out on the current build — so treat its sideslip numbers as directional. Nothing here has been flown.

## The same port as the Revoker's
Same merge (F-22E 2.4.16 → 2.6.6 on the Ifrit's controller), same retirements, same player section **"6. Lateral governor"** (`PullRollTrade` moved from the Dev file with your 0.7 carried over once; `RollStopSideslip` 4, `RollGovernorPitch` true, `RollPaceRateSlip` 1, `RollPaceRateBlend` 0.4, `RollStopSlipProbe` true), the five retired lateral keys dropped from the Dev file, the 2.4.18 yaw slew lead (Dev `YawSlewLead`). Telemetry after `gsFyK`: `stopSlipLim … tradeAlpha, yawCap`. The Ifrit never had CoordinatedStop or the F-22E's push + roll `slipGov` additions beyond 2.1.x; the 2.1.0 push + roll limiters are now damage-only (`negGate` 0), as on the Raptor. See `Revoker_FCS_0.3.0_notes.md` for the list of what came in and went out.

## Ifrit-specific: the yaw axis (`yawEffs`)
**The Ifrit has no rudders** — the handoff's "rudders only" was wrong (the two fins carry no control surface; see `Ifrit_FCS_explore_notes.md`). The F-22E's governor reads "the rudders" wherever it needs the yaw axis; merged as-is it found none, its capacity read zero and the rate cap sat on its 10 deg/s floor at every AoA. So:
- The yaw axis is **the canted all-moving tails' differential**, built each tick as one synthetic effector: the differential d about the symmetric (pitch) deflection s the pair has now, with the stops the pitch leaves it (L = s + d, R = s − d within each tail's ±30), the tails' slew, and the pair's own yaw table at the present flow, M(d) = M_L(s + d) + M_R(s − d). Plus **each split deceleron** within 0..max (the allocator's second tier, `SplitYawTier`). No schedule on the tails (their tables at the local AoA carry their stall); `rudderAuth` (50–60°, copied from the Raptor) keeps its other roles in the governor (the derate blend, the trade share, the open-loop cap hand-over).
- The allocator's `aYaw` was not used. On the calibrated plant against the 28 and 30 Oct logs (`eqerr yawsplit=1`: the logged pose with the splits closed and then the tails at their mean), the tails at 55–65° of differential delivered **3–28 deg/s²** (28 at −30..0° AoA, 10 at 15–30°, 3 at 45–90°) and the splits 0–3, where `aYaw` read **38–168**. It sums every effector's full yaw table (tails, ailerons, decelerons, splits) at its unstalled extremes.
- **The fin's own stiffness is credited** (`PaceStiffCredit` 1). The probe measures the airframe's directional stiffness k (`slipStiff`, deg/s² per deg of sideslip, from the airframe's tables at the present flow); with the fin in the reversal model (yaw error under constant effector yaw a plus the weathervane: β̈ + c k β = −c a) the sideslip peaks at the margin m for dR² = (2 a m + k m²)/c — the kinematic form with a → a + k m/2. Nothing entered beyond the probe's number and the margin. On the Raptor this is 6–12 % of the rudders and was never credited; on the Ifrit k is 4–6 at 480 km/h against 25–30 from the tails, a third to a half of the axis. The credit goes into the rate cap, the pull cap's yaw-left, the sweep/shed caps and the trade share (`yawCap` in the log includes it).
- **`SweepMarginGate`** (new, on): the sweep cap (every sweep other than a pull at positive AoA — pushes, pulls from negative AoA) fades in with the forecast sideslip over the margin exactly as the pull cap does (from 75 % of the margin), instead of standing at every sideslip. On the Raptor it stands ungated and paces a 118 kt push to ~30 deg/s; on the Ifrit the yaw axis at 108 kt is ~12 deg/s² and the ungated cap held a full push + full roll to **8 deg/s of AoA rate — 2 s to leave +4° AoA — at 1.6° of a 4° margin** (0.5.6: 48 deg/s, 3.4°). Gated: −22° at 2 s (0.5.6: −20°) at 4.0°. The shed cap (the rotation at the zero crossing) keeps its form.

Not ported from the Revoker: its roll-axis window changes (`PaceWeakAxis`, `PaceRollGate`, `PaceRollSlew`) — those came from the Revoker's strong-yaw / weak-roll asymmetry; on the Ifrit the yaw is the weak axis, as on the Raptor, and the Raptor's window rules stand.

## Bench, 0.5.6 → 0.6.0 (your config; margin 4, blend 0.4, trade 0.7)
**Stop / reversal sweep**, 32 cases 300–650 km/h × pull 0.25–0.7 (4–22° AoA):

| | stop worst / mean | reversal worst / mean | mean roll | crossing |
|---|---|---|---|---|
| 0.5.6 | 5.5 / 3.39 | 7.2 / 3.73 (4 of 16 not completed in 1.3 s) | 109 | 0.33 s |
| 0.6.0 | 4.7 / 2.38 | 7.2 / 3.24 (all complete) | 85 | 0.40 s |

Case by case: the 500 km/h / 12° reversal is the worst in both (7.2); two 300 km/h reversals at 8–12° got worse (3.2 → 4.2, 3.6 → 5.2) with faster crossings (0.28 → 0.23, 0.47 → 0.25) — the old coordinated-jerk / brake caps are gone and the rate cap's trim at 4–12° AoA (sin AoA small) is light; the 650 km/h reversals improved (7.0 → 4.9, 5.2 → 3.7) at −28 % roll. **The old stack ran over the 4° margin in 20 of 32 cases; the governor in 7.**

**Regression scenarios** (sideslip peak; roll mean):
- Push + roll: 200 km/h 3.4 → 4.0 at −22° vs −20° at 2 s (roll 81 → 53 max), 300 km/h 5.2 → 1.9 (−42 → −28 at 2 s), −55° entry at 170 km/h 2.9 → 4.0 (−13 → −20 at 2 s), roll-then-push 4.4 → 3.4.
- Pull-then-pushover + roll 350/450: 7.4/6.9 → 2.4/2.3; push-then-pull-through 5.5/5.7 → 2.1/2.5; pull-through with the roll released at zero 9.7 → 4.1; held 0.4 g + roll 4.0 → 3.3.
- Full-aft rolling pulls with a 3 s reversal: 6.6/6.2 → 2.1/1.6 at 85/95 → 52/71 deg/s peak roll.
- 65° stick release with a rotation on: 5.8/4.1 → 5.2/4.9.

**Corkscrews** (held pull + full roll, 6 s): the old stack made **5–9° of sideslip** at 45–72 deg/s of velocity-vector roll at 60°; the governor 1.2–4.5° at 30–38 deg/s. At 20–45° AoA: 5.6–7.8° at 82–134 → 2.2–3.1° at 52–112.

## Read this before flying it
Your Ifrit ran its rolling-pull governor with `PullSideslipMargin` **15** (your 0.3.1 setting, against 6–9 on the Raptor): the old stack's high-AoA rolls were making 5–9° of sideslip on the bench and you flew them that way. The governor's margin is now the Raptor's **4** (`RollStopSideslip`, player config). At 4 the Ifrit will roll noticeably slower above ~20° AoA than 0.5.6 did. That is your knob, not mine: if the 0.5.6 feel is what you want back, `RollStopSideslip` 8–10 is the first thing to try, `RollPaceRateSlip` the second (the Raptor's compromise points were found the same way).

## Carried over as-is, to re-check in flight
The Raptor's list (`Revoker_FCS_0.3.0_notes.md`) plus, new here, `PaceStiffCredit` 1 (0 = kinematic) and `SweepMarginGate` (false = the Raptor's ungated cap). The probe found no sideslip bound inside 20° anywhere on the bench (`slipTol` 25).

## Not done
- No closed-loop replay of your logs (no `replay` mode on the stock benches yet).
- The bench is thin: two or three more varied Ifrit logs on the current build are still the ask before leaning on its sideslip numbers (calibration doc).

## Install
- **Main:** `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 856773cf8d590bb9d865ba0a2283133b) and `meta.json`, version 0.6.0. Dev DLL unchanged.
- **Backup:** 0.5.6 kept as `…\Ifrit_KR67_FCS.dll.0.5.6.bak` (the old lateral stack, for A/B).
- **Release:** `C:\Games\NO mods\Ifrit-FCS-0.6.0-release` (`Ifrit-KR67-FCS-0.6.0.zip` first, `Source.Ifrit-KR67-FCS-0.6.0.zip` last; the source zip carries the bench with `isync.sh`, `tomifr.cfg` = your player + Dev config, `eqerr yawsplit=1`, `ifly … fcslog=<csv>`, and the `an/` scripts). The NOMM listing (`meta.json` in the plugin folder points at the GitHub release) is not updated — that is your release step.

## First flight — what to look at
1. Reversals at 10–25° AoA, 300–500 km/h: sideslip and crossing; `paceHold`, `paceLead`, `yawCap` (expect 25–45 at 300–500 km/h; the tails' share of it collapses as they stall).
2. A full push + full roll at ~110 kt: the push should run and the sideslip touch 4; `yawSatPitch` in the log says if a cap held it.
3. High-AoA rolls: the rate. If too slow, the margin knob (see above).
4. If the splits open in ordinary reversals (`splitL/R` in the log), they are the second tier doing the governor's bidding — tell me; that is drag you did not want.
