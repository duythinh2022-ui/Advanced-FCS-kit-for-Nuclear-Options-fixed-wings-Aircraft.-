# F-22E Strike Raptor FCS — handoff at 2.31.0 (3 Nov 2026, 17:55)

Start a new chat from this. It supersedes the status sections of `claude/Governor_fresh_start_handoff.md` (§0) and
`claude/Bench_leaf_calibration_handoff.md`; read those only for history. Tom's control-law requirements are in project memory
(the F-22E FCS area file) — read them before any law work.

## 0. State right now

**Installed and flown: 2.31.0** — `C:\Program Files (x86)\Steam\steamapps\common\Nuclear Option\BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\`
`Aryx_F22E_StrikeRaptor_FCS.dll` (653 312 bytes, md5 `63688cb1b2697a1135ec902482b7d8d4`), `meta.json` 2.31.0. Backups in the same
folder: `.dll.2.30.0.bak` (`95d20827…`), `.dll.2.27.0.bak` (`d8c1ce43…`), 2.29.0, 2.28.0, 2.26.0, 2.25.0. Release folders
`C:\Games\NO mods\F22E-FCS-2.2x/2.3x.0-release` (DLL, Dev DLL, game zip, source zip, notes, meta.json, suite diffs).

**Tom's verdict on 2.31.0 (17:43 log):** "snap roll is much less wobbly, within acceptable range; release push-over is good; held
push-over pos → neg is good, neg → pos still has a slight hitch but acceptable." This is the first build today he accepted on every
push-over case. **Do not change push-over behaviour without a reason he gives.**

**Source of record:** `C:\Games\NO mods\F22E-FCS-2.31.0-release\F22E_FCS_2.31.0_src.zip` (the FCS + the leaf-calibrated bench, see §4).
`C:\Games\NO mods\F22E-bench-portable.zip` is the same tree (portable bench for WSL2 on his PC; `bash bench.sh build` first).

**What 2.31.0 is:** the 2.27.0 law (the push-over devices of 2.24–2.27, §2) + 2.30.0 (air data at the CG) + 2.31.0 (shaped released-stop
yaw brake). 2.28.0 / 2.29.0 (the "single reference-capacity governor" consolidation) were flown and rejected ("rougher", "worse and
worse") — their structure is in their source zips if ever wanted; the released weight there flickered (t_yaw / t_roll per tick).

## 1. What was found and fixed on 3 Nov evening

### 2.30.0 — air data at the CG (`AirDataAtCg`, `FlightPathAtCg`; false = 2.27.0 byte for byte)
`ac.rb` is the **root part's** rigidbody, CoM **2.48 m ahead of the aircraft CG**. Every earlier build read AoA / sideslip / TAS /
flight-path rate from it, carrying ω × 2.48 m (r·2.48/V of sideslip: 1.2–2.0° in a 60–75 deg/s rotation at 130–200 kt). Now `vCg` =
mass-weighted velocity of all bodies (clean: `beta − betaRoot` = r·l/V with 0.05° residual in flight). `nz` stays the root body's
(G-loop feel; a separate decision). Model copies of the mod's downwash hook keep the root velocity the hook itself uses. CSV columns
`alphaRoot, betaRoot, airRefCg` (=1). Flown 17:07: no consistent effect on its own; Tom's "more wobble" turned out to be §1.2, also on 2.27.0.

### 2.31.0 — the nose wobble (`ReleaseBrakeShaped`, `ReleaseBrakeTdMin` 0.15; false = 2.30.0 byte for byte)
Cause: the 2.25.0 `ReleaseFollowYaw` brake request rCmd = r − capacity / KYaw. On snap roll+pull releases at 16–28° AoA, 300–400 kt the
capacity is 100–150 deg/s², so the request sat 40–60 deg/s **past zero yaw** (r 7 → command −52); rudders (100 deg/s) need ~0.35 s
back from the stop; yaw overshot to −23…−30, the follow let go in one tick (yaw-accel target −240 → +204), on 2.27.0 it even re-engaged
the other way: sideslip ±9–12° at ~1 Hz. Fix: request = min(capacity, |r| / t), t = rudders' travel to neutral / servo rate.
Flown, same detector on all three flights (snap releases 11–28° AoA, 278–414 kt):

| build (log) | n | yaw back-swing median / max | sideslip p-p median / max |
|---|---|---|---|
| 2.30.0 (17:07) | 4 | 24 / 30 deg/s | 13.3 / 18.7° |
| 2.27.0 (17:27 re-fly) | 5 | 21 / 36 | 11.1 / 18.3 |
| 2.31.0 (17:43) | 11 | **3 / 8** | **4.9 / 5.9** |

Tried and not shipped (deleted from source): `ReleaseBrakeToZero` (brake exactly to zero — back-swing gone, first swing larger);
`SweepMarginCredit` (kinematic sweep cap credited with the remaining sideslip margin from *measured* sideslip — the slowdown went but
the sideslip arrived too late: bench held push 3.4 → 11.8° at the bottom).

## 2. The law today (2.27.0 + the two changes) — the push-over devices still in it
Kinematic sweep cap (`SweepYawK` 0.7, `SweepPerAxis`, `SweepRollShare`, sized on max(|pTraj|, measured pS)), shed cap (`SweepShedK`,
`SweepShedMeasFloor`, `SweepShedNoStab`, `SweepShedCos`, `SweepShedSlip` = `PushoverSideslip` allowance), delivered term
(`SweepDelivered`/`SweepDelTau`), crossing lead (`SweepCrossLead`), cap filter + slews (`SweepCapTau`, `SweepCapSlewDn/Up`), zero floor
(`SweepNoReverse`, held too via `SweepNoReverseHeld`), the released follow (`ReleaseFollowYaw` with dwell, push-intent engagement,
p0 bound `ReleaseFollowCapPS0`, pace `ReleaseFollowPaceK` 1.5, fin-health power, `ReleaseDropSweep*`), the trade (`PullRollTrade` 0.7,
`TradeCapShare` 0.7) on the sweep that grows |AoA|, the 2.19.0 no-fin hold, 2.20.0 no-fin stop. Tom has called the 2.24–2.27 additions
"on-top"; they now fly acceptably. Removing any of them is a law change he must ask for.

## 3. Open items, in Tom's priority (none urgent — he accepted 2.31.0)

1. **The airframe's passive lateral stiffness is still not in the governor** (Tom, 17:51: "so airframe stiffness is still not taken
   into account with the gov yet?" — correct). This is his standing spec since 2 Nov 01:30 ("the airframe is balancing at that AoA
   laterally") and the next piece of work. Facts:
   - Logs (§1a of the bench handoff, quick regression) and the calibration agree: the sideslip is held by the **dihedral effect, as
     roll**: stability-axis kβ = Nβ cos α − Lβ sin α ≈ 6.0 / 4.0 / 5.0 deg/s² per deg per 1e4 Pa at 45 / 55 / 65° (game; bench 6.1 /
     3.6 / 4.9). Body-axis Cnβ is ≈0 at 40–60° and negative above 60° (canted fins stalled).
   - The governor sees the airframe only as the **measured** acceleration now (`CapacityEstimator` `wdZ/wdX`). The only passive term,
     2.7.2 `slipStiff` (`FcsController.cs` ~3200–3260, `SlipMomentAt(α, ±4°)`), keeps the **body-yaw part only** — medians in flight
     2.1 / 1.0 / 0.2 / −0.2 / −0.1 at 5–15 / 25–35 / 35–45 / 45–55 / 55–70°, clipped at 0 → nothing above ~45° — and feeds only the
     roll-reversal rate cap (`aOpp`). The roll part is in the same `Vector3` and discarded.
   - The probe under-reads kβ 3–4× above 50° even with both parts (`bench slipprobe`: 3.4 / 1.9 / 1.3 vs logs 6.0 / 4.0 / 5.0): it sums
     raw part charts in uniform flow — no ω×r at the parts, no per-part force clamp, and (to verify) none of the mod's LiftShape /
     BodyLift / camber hooks the game applies. The bench `Plant.Forces` has all of them.
   - The +70…96 deg/s² self-roll through 50 → 40° on a released push is **not** Lβ·β (β < 1° there); it is the sweep's nonlinear
     quasi-steady aero (per-panel AoA under r and α̇ on post-peak curves). A stiffness term will not carry it; only the measured term
     or a full table evaluation at the predicted state does.
   **Plan (each step bench-checkable before it drives anything):** (a) fix the probe to evaluate the airframe as the plant does
   (rotation at parts, clamp, hooks) and log both parts + kβ as Dev telemetry; validate against the calibration numbers above
   (within ~20 %, roll and yaw separately); (b) the passive capacity = kβ × max(0, `PushoverSideslip` − |β| on the helping side),
   capped at the stall knee, as one logged term (`cePass`) in the **single** capacity read so every consumer gets it (2.7.2's mistake
   was one consumer); (c) **predictive, not reactive** — the measured-sideslip credit failed (sideslip shows too late); (d) first
   consumer: the kinematic sweep cap (the held neg → pos hitch, §3.2). One law change per build.
2. **Held push neg → pos slight hitch** (Tom: acceptable). 17:07 log 122.0 s (109 kt, −50°): command +20 → 0 for 0.35 s at −38…−30°;
   held push pos → neg 86.7 s (168 kt) the same at 31–23° on 2.30.0 (Tom finds pos → neg good on 2.31.0). Binding: `capSwp` 40 → 18
   sized on the measured pS 60–66 (the airframe's own roll) while the target is 33; sideslip ≤ 2.3° of 4. Fix = §3.1 (d).
3. **First sideslip swing on snap releases** (9–12° on the hardest 2.30.0 snaps, 2.31.0 p-p ≤ 5.9° — within Tom's acceptable range):
   the roll stops 2–3× faster than the rudders can reverse. Place: the rate cap's stop model (governor handoff §4 a) with the rudder
   transit as its dead time. Tom's rule: cap the commanded roll rate only, never the roll acceleration / deceleration.
4. **Full pull+roll reversal at ~25° / 337 kt → 19° sideslip** (17:07 log 43.1 s; bench reproduces it, 19.7°; same on the old reference).
   The 2.27.0 rate cap / reversal model. Bench case "pull+roll rev@0.7 450" 16.8 → 20.2 with 2.30.0's CG reference.
5. Bench: the pitch-in-the-sweep mismatch (calibration results §4/§5: the plant under-predicts nose-down pitch acceleration in the first
   second of a released push by 7–9 deg/s²; `replaycheck` fails the q criterion on 8 of 9 2.27.0 pushes). Push-over pitch feel is not
   judgeable on the bench until it closes; mid-AoA lateral replays are good (β RMS 1–3°).
6. The rudder ±35 command flip tick to tick (every AoA in the game; not reproduced on the bench).
7. Later: retire the governor's remaining on-top devices (tally from 15:2x: 3 structural, 8 on-top), the old lateral code archive,
   and the Ifrit / Revoker ports (their benches got the root-reference fix; neither has a push-over log set).

## 4. Bench (in the 2.31.0 source tree)
- Leaf calibration (other chat, `bench/CALIBRATION_leaf.md`, project `claude/Bench_leaf_calibration_results.md`): plant already right at
  the leaf laterally (eqerr |α| ≥ 40° roll 9–10, pitch 10–12, yaw 2.1–2.5 deg/s²); `EqErr.LogRefRoot` (logs are root-referenced);
  `Plant.DCnbHi = −0.4` (the one added term); `bench slipprobe`; `bench.sh replaycheck <tag> <logdir>` with `bench/calib/leaf_cases.json`.
- 2.30.0 additions: logs with `airRefCg = 1` are posed at the CG automatically (`Replay.cs`, `EqErr.cs`); replay `alpha_b/beta_b` use
  the log's reference (`alphaRoot/betaRoot` for older logs). **Suite `alpha/beta` columns are now the plant CG's (`CgAlpha/CgBeta` in
  `Program.cs`)** — suite outputs from trees before 2.30.0 are not comparable; re-run the baseline in the same tree (2.27.0 baseline tag
  `v227c`, 2.30.0 `v230c`, 2.31.0 `v231`).
- Replay logdir must hold the logs under their game names (`F22E_FCS_20261103_HHMMSS.csv`); `replaycheck` counts only logs of the
  same build as the source.
- Useful scripts (also at `C:\Games\NO mods\F22E-analysis-scripts\`): `wob.py <logs>` (snap-release detector: yaw back-swing and
  sideslip p-p — the §1.2 table), `relstop.py` (roll releases at 8–40° AoA), `findpush.py` (push-overs from > 45°), `stiff_quick.py`
  (per-band Lβ / Nβ / kβ regression), `bench/calib/derivs.py` (game vs bench derivatives on an eqerr dump).
- Pattern that works: replay the flight's own events from their logged states with the new law and with the change switched off
  (`Name=value` statics), compare per event; then the full suite in the same tree.

## 5. Logs (Tom's PC, `…\Nuclear Option\BepInEx\`)
3 Nov: 10:40 (2.21.0), 11:19 (2.22.0), 12:16 (2.23.0), 13:06 / 13:12 (2.24.0), 13:59 (2.25.0), 14:23 (2.26.0), 14:45 (2.27.0), 15:13
(2.28.0, fins off after ~175 s), 15:25 (2.29.0), **17:07 (2.30.0)**, **17:27 (2.27.0 re-fly)**, **17:43 (2.31.0)**. Push lists per log
in `claude/Bench_leaf_calibration_handoff.md` §2 and `bench/calib/leaf_cases.json`. 309 columns = 2.30.0, 310 = 2.31.0 (`relBrakeTd`).

## 6. Tom's rules (binding)
Fixes inside the laws, no on-top devices, no gates standing in for a physical quantity, no push-over-only special cases; his tuning
values untouched (`PullRollTrade` 0.7, `TradeCapShare` 0.7, `RollPaceRateBlend` 0.4084507, `RollStopSideslip` 4, `RollPaceRateSlip` 1,
`RollGovernorPitch` / `RollStopSlipProbe` on, `PushoverSideslip` −1 = 4°); the governor lowers a command only when an axis can't keep
up, counting passive airframe capacity and the trade; never slows roll acceleration / deceleration for sideslip; push-overs: one
continuous slowdown and recovery, no step / plateau / nozzle reversal / positive command with the stick forward, sideslip in check;
minor version bump per law change; release folder + install with `.bak`; say "compiled / bench-only / flown" with numbers; imperial
units; one change at a time; never overstate verification; don't predict sideslip from the rudders alone; the sideslip-first allocator
priority was tried before and made everything "very stiff and not universal" — don't.

## 7. What of the old governor is still not replaced (2.31.0 source, checked 17:57)
Since 2.31.0 is the 2.27.0 law, the push-over stack that 2.28.0 deleted is back in. Replaced: the capacity estimator and its pitch-side
consumers (the trade, the deleted AoA-rise cap). Still on the old model or on top:
- **Structural, old model (3):** the steady-rate cap's reversal model (2.5.8/2.6.9 swing model, `RateCapNeedTravel`; only its capacity
  number is the estimator's — the 4 FAIL / 9 utilisation bands, handoff §4 a); the pace window's band and reach (`PaceWindow`, `paceHold`,
  2.7.x, §4 b); `coupRollLim` (coupling roll cap, table version).
- **Push-over stack, 2.24–2.27 (6 devices, on top):** kinematic sweep cap (`SweepYawK`, sized on measured pS — the neg → pos hitch);
  shed cap (`SweepShedK` family + `PushoverSideslip` allowance); delivered term (`SweepDelivered`); crossing lead (`SweepCrossLead`);
  cap filter + slews (`SweepCapTau`, `SweepCapSlewDn/Up`); the released follow (`ReleaseFollowYaw` + dwell, p0 bound, fin power,
  pace, drop-sweep; 2.31.0 shaped its brake). Plus the zero floor `SweepNoReverse` written as a clamp.
- **Older on-top devices (8):** `DamageLatLimit` fin leg + `FinLossAoACap`; `slipGov` + yaw anchor (asym-gated, idle on a clean jet);
  2.0.12 `YawTargetClamp` + stab reach; 2.2.7 flaperon split; 2.4.17 no-chase; 2.4.18 yaw slew lead (`YawSlewLead`); 2.19.0 no-fin
  nose-hold (`SweepHoldFade`) + 2.20.0 no-fin stop (`PaceStopNoFin`); 2.3.0 `CoordinatedStop` (released-stick stop paced by the lagging
  axis, high AoA). `RudderWindowSweep` is still in source, off.
- **Not devices:** the passive airframe stiffness (§3.1, the missing input); the capacity read collapsing under the roll loop's own
  unmet; the rudder ±35 flip.
- **Housekeeping:** the retired lateral governor code still interleaved in `LateralLaw`; the Ifrit / Revoker ports of everything since 2.8.0.
