# Revoker FCS 0.3.0

Base: **0.2.22**. The F-22E's reference governor (2.6.6) ported, with the rolling-pull governor and the rest of the old lateral sideslip stack retired the way F-22E 2.6.0 did. Minor bump: control-law addition and removal.

**Bench-only. Not flown.** Compiled against the game assemblies; every number below is from the calibrated Revoker bench (`Revoker_bench_calibration_0.2.15.md`), which this build's bench reproduces to the digit on the held-out 30 Oct 22:48 log (pitch 6.8 / yaw 3.0 deg/s², nz 0.30 g RMS).

## What moved in from F-22E 2.4.16 → 2.6.6 (merged, not retyped)
- **In:** the reference governor — rate cap + yaw-reach window blended by `RollPaceRateBlend`, the roll-first-then-pitch caps (`RollGovernorPitch`: pull cap with the 2.6.3 forecast, sweep cap, 2.6.6 shed cap), the 2.6.4/2.6.5 trade split (`PullRollTrade` 0.7 = roll 70 % / pitch 30 % on the demand-growing sweep only), the single margin with the 2.5.15/2.5.16 airframe probe (`RollStopSlipProbe`, telemetry `slipTol/slipDep/slipAuth/slipStiff`), the 2.4.18 yaw slew lead (`YawSlewLead`, Dev), the 2.6.1 paced window release.
- **Out (as 2.6.0):** AoA-sweep yaw limit, the 2.2.7–2.2.29 rolling-pull sideslip governor and its boost/servo, `PullSideslipMargin`, `PullFFBoostMax`, `PullGovSmooth`, RollEntryCoord / BrakeYawCap / RevYawCap, the coordinated jerk, and the 2.1.0 push + roll limiters for the clean jet (`negGate` 0 — they stay for the asymmetric-load case). The Revoker's own 0.2.14/0.2.15 push + roll sideslip governor (`slipGov`, `SlipGovLeadFromLoops`) rode on that gate and is therefore off for the clean jet too; `SlipGainFromLoops` (the sideslip-hold gain from the loops' lag) is the loop, not a limiter, and stays.
- **Config:** new player section **"6. Lateral governor"** — `PullRollTrade` (moved from the Dev file; your 0.7 carried over once on first launch, `LogOutput`: "Revoker FCS 0.3.0: lateral governor keys moved…"), `RollStopSideslip` 4, `RollGovernorPitch` true, `RollPaceRateSlip` 1, `RollPaceRateBlend` 0.4, `RollStopSlipProbe` true. The five retired lateral keys are dropped from the Dev file.
- **Telemetry** after `slipLead`: `stopSlipLim, stopSlipAB, stopSlipU, stopSlipPred, stopSlipMargin, stopSlipHold, yawSatPitch, yawLead, paceLead, paceHold, slipTol, slipDep, slipAuth, slipStiff, paceRel, tradeAlpha, yawCap, rollGate`.

## Revoker-specific: the yaw axis the governor counts on (`yawEffs`)
The F-22E's governor reads "the rudders" wherever it needs the yaw axis — its tables, crossing time, load, authority at hypothetical sideslip. On the Revoker that set is **the rudder (faded with the fin, `rudderAuth`, 75–85°) and the yaw nozzle** (thrust: holds at any AoA and IAS, its own ±15° stops and 70 deg/s slew). Every rudder loop in the governor runs over that set; the crossing time of a reversal is the yaw-weighted mean of the two effectors' crossings.

The allocator's `aYaw` was **not** used as the capacity. Checked on the calibrated plant against four 30–31 Oct logs (`eqerr yawsplit=1`: the same logged pose with the rudder and nozzle at neutral): with both at their stops the two deliver **58–91 deg/s²** (79–89 at −20..−55° AoA, 58–64 at 20–90°), while the FCS's `aYaw` read **101–168** at the same samples — it sums the canards', elevons' and splits' yaw tables as well, which the airframe did not deliver (the plant's total yaw acceleration matched the logs to 2–9 deg/s² RMS there, so the two effectors are the yaw). This is the "aYaw 2× too loose" finding of the 0.2.15 notes; its fix is the effector set, not an effectiveness estimate. The sweep cap, shed cap and the trade's free-yaw share use the same capacity (`yawCap`).

## Revoker-specific: the roll axis in the window
Two changes inside the pace window, both from one bench case — full-aft rolling pull at 320 km/h reversed at 69° AoA / 80 m/s (reg `fullaft+roll+rev3s 320`):
- With the F-22E's rules as-is the window stayed shut (the rudder at −5 and the nozzle at −1 — the yaw effectors' load gate never opened), the yaw nozzle reversed the yaw 39 → −30 deg/s in 0.6 s while the canards were swapping sides at 100 deg/s and the body roll ran the wrong way for 0.4 s: **15.1°** of sideslip (0.2.22: 3.3). On the F-22E the yaw is the weak axis nearly everywhere; here the nozzle is the strong one at high AoA and the stalled wing the weak one.
- **`PaceRollGate`:** the roll effectors' slew in use (share of their rate, weighted by one-side roll, 0.08 s filter) opens the window over `PaceLoad0..1` like the rudders' travel does. Not their position (the canards sit pinned by the pitch trim in every steady corkscrew; a gate on that held the 68° corkscrew to 31 deg/s) and not the stick's demand (same effect by the other route — both tried).
- **`PaceWeakAxis`:** with the yaw and roll windows disjoint, the window of the axis that can move less toward the target from the roll the jet has stands (the F-22E keeps the yaw window). The sideslip rate while the two disagree does not depend on the target; the target only decides which axis runs ahead, and the strong one waiting is the rule's own meaning.
- **`PaceRollSlew`:** the roll window's reach is the roll the roll effectors can add within the horizon from where they are at their own slew and within their allocator windows (the canards' attached-flow window), as the yaw window's reach is for the rudder and nozzle (F-22E: `aRollPos × horizon`, all of it at once; its roll surfaces cross in 0.25 s, the canards cross 105° at 100 deg/s).
Result on that case: 2.5°. All three are statics (`Name=value` on the bench, `false` = the F-22E rule).

## Bench, 0.2.22 → 0.3.0 (Tom's config; margin 4, blend 0.4, trade 0.7)
**Stop / reversal sweep**, 32 cases 300–650 km/h × pull 0.25–0.7 (6–27° AoA):

| | stop worst / mean | reversal worst / mean | reversals 10–22° worst / mean | mean roll | crossing |
|---|---|---|---|---|---|
| 0.2.22 | 4.2 / 2.57 | 7.9 / 3.91 (4 of 16 not completed in 1.3 s) | 5.5 / 3.45 | 93 | 0.41 s |
| 0.3.0 | 2.9 / 1.92 | 4.6 / 2.33 (all complete) | 4.6 / 2.79 | 84 | 0.51 s |

**Regression scenarios** (sideslip peak, deg; roll mean, deg/s):
- Push + roll corkscrew entries (Tom's open complaint): 200 km/h 1.3 → 0.6, 300 km/h 3.4 → 0.7, −55° entry at 170 km/h 1.4 → 0.3, roll-then-push at 200 3.1 → 1.3. Cost: the roll 44–65 → 32–39 deg/s and the AoA at 2 s −37/−41 → −24/−28 (the trade and the sweep cap). The F-22E 2.6.6 on its own bench runs the same 300 km/h case at 56 deg/s / −23° at 2 s, so this is the governor behaving as on the Raptor, not a Revoker-specific hold-up.
- Pull-then-pushover + roll 350/450: 4.5/4.0 → 2.0/2.2; push-then-pull-through 3.5/2.8 → 2.6/2.5; held 0.4 g + roll 350: 3.0 → 2.6.
- High-AoA rolling pulls (65–69°): 1.1–1.9 → 1.0–1.4, body roll 23–27 → 13–16 deg/s.
- 65° stick-release with a rotation on: 3.0 → 3.8/5.2 (the canards pinned at both stops by the full-aft pitch trim, no roll for the sideslip correction; the governor acts only on commanded roll — not its doing, noted).

**Corkscrews** (held pull + full roll, 6 s): sideslip peaks 1.5–3.7 → 0.9–3.4 and RMS down everywhere; the steady velocity-vector roll at 65–68° AoA **75–84 → 46–55 deg/s** (350–550 km/h) and **75–80 → 34–36** at 250 km/h; AoA wobble p-p 7–11 → 16–18 at full pull (slower roll, longer breathing). That is the rate cap (blend 1 alone gives 17 deg/s there; blend 0 gives 61–70) plus the window: the Raptor paid 80 → 56 in the same corner and Tom accepted it; the Revoker pays more because its yaw axis at 65° is the nozzle alone (≈75 deg/s² against a 90°-swinging demand). The knobs are his three (`RollPaceRateBlend`, `RollStopSideslip`, `RollPaceRateSlip`): slip 2 gives 37–39, blend 0 gives 61–70 at 2.2 of sideslip.

## Carried over as-is, to re-check in flight (least physics first)
`PaceLoad0/1` 0.5/0.75 + `PaceGateTau` 0.3 (now also the roll gate's thresholds), `SweepShedCos` 0.5, `StopSlipPitchStart` 0.75, `SweepShedK` 0.6, `SweepShedDead` 5, `PaceYawUse` 0.85, `PullTradeFreeK` 0.35, `PaceRateK` 1, `PaceFloor` 4 / `PaceHorizon` 0.15, `StopSlipPinAB0/1` 40/55 (the pull cap's fade-in by the yaw capacity — always on here, the nozzle keeps the capacity above 55 at any IAS), `StopSlipA0/A1` 36/45 (the one-axis band). The probe found no sideslip bound inside 20° anywhere on the bench (`slipTol` 25), as on the Raptor.

## Not done
- No closed-loop replay of the 30 Oct 23:45 / 23:55 corkscrew entries: the stock benches have no `replay` mode yet (the Raptor's needs the Poser's engine/throttle solve ported). The synthetic −55° entries above stand in.
- The Ifrit port is a different animal — see its notes.

## Install
- **Main:** `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 6b903fd2ef0525b8f90d258b41405ee3) and `meta.json`, version 0.3.0. Dev DLL unchanged (`Revoker_FS12_FCS_Dev.dll`, the unlock only).
- **Backup:** 0.2.22 kept as `…\Revoker_FS12_FCS.dll.0.2.22.bak` — the A/B for the old lateral stack.
- **Release:** `C:\Games\NO mods\Revoker-FCS-0.3.0-release` (`FS12-Revoker-FCS-0.3.0.zip` first, `Source.FS12-Revoker-FCS-0.3.0.zip` last; the source zip carries the bench with `rsync.sh`, `tomrev.cfg` = your player + Dev config, the `eqerr yawsplit=1` mode, `ifly … fcslog=<csv>` for the FCS's own telemetry, and the `an/` scripts: `ssweep.py`, `sreg.py`, `cork.py` with `STOCK=1`).

## First flight — what to look at
1. Reversals at 10–25° AoA, 300–500 km/h: sideslip and the crossing (0.15–0.3 s is the Raptor's accepted band); `paceHold` and `paceLead` in the log.
2. The −55° push + roll entries at 90–110 kt: smooth now, or slow? `yawSatPitch` / `tradeAlpha` say which cap held the push.
3. 65° corkscrews: the roll rate. If it is too slow and the sideslip is far under 4, the three knobs first.
4. `rollGate` and `yawCap` in the log — the two Revoker-specific signals.
