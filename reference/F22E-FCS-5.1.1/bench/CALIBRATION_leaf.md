# F-22E bench calibration at the leaf (3 Nov 2026, 15:40–16:30)

Bench-only. No FCS behaviour change, no tuning value touched, nothing installed. Source: the 2.29.0 tree. Data: the 3 Nov
logs 10:40 … 15:25 (2.21.0–2.29.0; ~40 released and held push-overs from 65°, leaf and corkscrew at 45–66°) plus the 2 Nov
15:59 leaf. Train: 13:06 / 14:45 / 15:25; held out: 13:12 / 14:23 / 15:13 (before the fins came off at 170 s).
Everything below is from the bench and the logs — nothing here was flown.

## 1. What the plant already did at the leaf (before any change)

Equation error (`eqerr`, 31 Oct method; deg/s²), all 3 Nov logs: roll 12.7, pitch 16.9, yaw 3.2, nz 0.24 g — the same as the
31 Oct held-out numbers at low/mid AoA (11.9 / 16.3 / 3.07). By band:

| | 40–55° | 55–70° | <35° |
|---|---|---|---|
| train (13:06/14:45/15:25) roll / pitch / yaw | 13.0 / 13.0 / 2.4 | 8.3 / 10.5 / 3.1 | 14.0 / 19.2 / 3.3 |
| held out (13:12/14:23/15:13) | 11.5 / 11.4 / 2.0 | 8.2 / 8.1 / 2.5 | 13.6 / 17.8 / 3.6 |

Per-AoA-band control derivatives, game vs bench (`calib/derivs.py` on the eqerr dump), 40–70°: rudders 0.86–0.99, differential
stabs 0.92–1.09, ailerons 0.98–1.03, flaperons 0.99–1.01, yaw damping 0.94–1.21, roll-from-yaw-rate 1.01–1.07, Lβ (roll per
sideslip) 1.00 / 1.09 / 1.02 — the bench has the dihedral effect at the leaf. The handoff's prediction that the bench's Lβ is
low at high AoA is **not** borne out; its 2× sideslip came from §2.

The only yaw term off: body-axis Cnβ (per deg of sideslip per 1e4 Pa) — game +0.27 / +0.05 / −0.86 at 40–50 / 50–60 / 60–70°
vs bench +0.78 / +0.36 / −0.46. The jet is directionally neutral in body axes at 40–60° and unstable above 60°; the bench was
still stiff at 40–60°. The sideslip is held by Lβ sin α (handoff §1a): stability-axis kβ = Nβ cos α − Lβ sin α, game / bench:
6.0 / 6.1 at 45°, 4.0 / 3.6 at 55°, 5.0 / 4.9 at 65° (per 1e4 Pa) — within 10 % before the change.

Pitch: TVC pitch power reads 1.06 / 1.17 / 1.29 (game/bench) at 40–50 / 50–60 / 60–70° and pitch damping 1.27–1.69. See §5.

## 2. The reference-point bug (fixed: `EqErr.LogRefRoot = 1`)

The game's logged `alpha`, `beta`, `tas` are the **root rigidbody's** velocity (`ac.rb` = the root part, whose CoM sits
(0, 0.06, −0.03) in the root frame — 2.48 m ahead of the aircraft CG at (0, 0.25, −2.51)). The bench posed the CG at that
velocity. Evidence: the rudders' logged local AoA (`Rudder_*_aoaNow`) disagreed with the bench's by −0.024 / +0.024 deg per
deg/s of yaw rate (opposite on the two canted fins) and the stabs' by −0.025 deg per deg/s of pitch rate at 40–70° — exactly
2.48 m / TAS. With the pose corrected (CG at v − ω × d, so the root body sits at the logged velocity) the residual is 0.04°
(rudders) / 0.07–0.14° (stabs) with no rate dependence; the 31 Oct "≤ 0.06°" check had been done at low rates only.

Consequences: (a) in a 60–75 deg/s rotation at 60–70 m/s every replay used to start with 1.2–1.5° too much sideslip and the FCS
in the bench read β inflated by r·2.48/TAS throughout — the "bench sideslip ~2× the game's" known since 2.7.x; after the fix
β at t0 is exact and the replay β error over 2.5 s roughly halves (33.95 s push 0.5 → 0.4 RMS, 83.2 s 2.0 → 1.3,
141.0 s 0.8 → 0.4). (b) Equation error |α| ≥ 40°: roll 10.9 → 9.8 (train), 10.1 → 9.1 (held out); yaw 2.8 → 2.5 / 2.2 → 2.1;
pitch unchanged; <35° roll 14.0 → 14.4 (the 31 Oct factors were fitted with the old pose and absorbed some of it — not refitted).
(c) The bench's own closed-loop cases are unaffected (the FCS reads the root body's velocity in both game and bench).
`LogRefRoot=0` restores the 31 Oct pose byte for byte.

## 3. The one term added: `Plant.DCnbHi = −0.4`

Bench-only body-yaw moment DCnbHi × β(deg) × q̄/1e4 deg/s², blended in over body |α| 35 → 45° (`Airframe.MisHiA0/A1`). Checked
first that no part multiplier can produce the game's Cnβ: the fins and rudders are already near-neutral at 60–70° local AoA
43–51° / 28–36°, and the scan of `MisFinHi, MisRudHi, MisWingHi, MisBodyHi, MisStabHi, MisAilHi, MisFlapHi` (new, AoA-blended,
default −1 = inactive) moved the leaf error by at most ±1 deg/s² and mostly for the worse. With DCnbHi: bench Cnβ +0.43 /
−0.04 / −0.87 vs game +0.27 / +0.05 / −0.86. RMS unchanged (the term is ~0.5 deg/s² at the leaf); it matters for the sideslip's
drift over seconds, not the accelerations. `DCnbHi=0` = off.

## 4. Replays (closed loop, same law as the log: 2.29.0 source on the 15:25 log)

Held push 141.0 s: α / β / p / q / r within 1.7 / 0.4 / 2.2 / 5.0 / 1.4 RMS over 2.5 s. Released pushes 33.95 / 56.85 / 83.2 s:
first 1.0 s within 0.8–3.7 (α) / 0.3–0.5 (β) / 2–5 (p) / 5–6 (q) / 2 (r); after ~1 s the bench's body pitch rate lags the
game's (q error 10–16 RMS over 2.5 s) and the law reacts to that state differently (weaker `q_cmd`, higher body roll).

## 5. Open item: pitch during the 65 → 25° sweep

Open loop (exact game surfaces and state), the plant under-predicts the nose-down pitch acceleration through the first second of
most released pushes: bias +7.1 / +9.4 / +7.6 deg/s² (33.95 / 56.85 / 92.3 s), RMS 11–20, integrating to 7–10 deg/s of body
pitch rate — the size of the replaycheck q failures. The TVC pitch derivative ratio (1.17–1.29 at 50–70°) and the pitch
inertia scale 1.24 (a low-AoA compromise for `MisStab` 0.72) point at the nozzle's pitch power; a free refit of
`IScalePitch, MisStabPitch` over all 3 Nov AoA lands at 1.09 / 0.71 and gains nothing on the held-out logs (q 15.8 → 16.0),
`IScalePitch=1.0` alone makes the steady leaf worse (12.4 → 15.4). The steady-leaf moment balance and the transient disagree
about the inertia; not resolved. Candidates not yet tested: the stab downwash transient during fast α̇ (the wake-exit model
evaluated instantaneously), the nozzle thrust during the sweep (`tw` is logged, so the thrust is exact — the arm or inertia is
not), per-part unsteady terms the game may have. **This is what fails the §5 q criterion, bench and law alike.**

## 6. The FCS's table probe vs the logs (`bench slipprobe`)

`FcsController.SlipMomentAt(α, ±4°)` (the 2.7.2 `slipStiff` source) evaluated at logged states, per 1e4 Pa:

| AoA | probe Lβ | probe Nβ | probe kβ | logs kβ (derivs) |
|---|---|---|---|---|
| 0–10 | −6.3 | +2.8 | 3.5 | 5.4 |
| 20–30 | −5.2 | +2.6 | 4.4 | 5.0 |
| 40–50 | −3.4 | +1.4 | 3.4 | 6.0 |
| 50–60 | −2.0 | +0.4 | 1.9 | 4.0 |
| 60–70 | −1.3 | +0.25 | 1.3 | 5.0 |

The probe under-reads kβ by 3–4× above 50° (its Lβ falls to −1.3 while the logs hold −5 to −6). It sums the raw per-part charts
(`PartForce`) at a uniform flow — no rotation at the parts, no force clamp, and (to be verified) none of the mod's own
LiftShape / BodyLift / camber reshaping that the game applies through the hooks and the plant reproduces. **D cannot use the
probe at the leaf as it is**; find the missing hooks first.

**Correction (3 Nov, 18:30, 2.31.2 — `claude/Passive_lateral_stiffness_finding.md`):** the "logs kβ" column above is the β
coefficient of the per-band regression, not a derivative. The same regression on the plant's own predictions gives roll-β
−6.6 / −5.2 / −4.6 at 45 / 55 / 65° where the plant's exact derivative (`bench sliptruth`, Plant.Forces at β ± 2°) is −2.4 / −1.1 / −0.2.
The static kβ is 1.0 / 0.4 / 0.04 deg/s² per deg at the flight q̄ (45 / 55 / 65°), and the probe **over**-reads it (2.2 / 1.2 / 0.6).
What balances the jet laterally at high AoA is the rate coupling (Lr 0.6–0.85, Lp −0.7 to −0.8 per deg/s). The game/bench
ratios above stay valid.

## 7. The self-roll (handoff §1, 12:16 log 26.25 s)

The +84…+96 deg/s² body roll through 50 → 40° is reproduced by the plant to within 15 % (predicted 67–81). It is **not**
Lβ·β: β is 0.3–0.6° there (Lβ·β ≈ −1). The linear per-band terms (Lβ, Lp, Lr and the four effector groups) sum to only 25–36;
the rest is the nonlinear quasi-steady aero of the sweep (per-panel local AoA under r and α̇ on the post-peak lift curves),
which the plant has and a stiffness schedule does not. For the estimator it is reachable only through the measured term or a
full table evaluation at the predicted state.

## 8. The rudder ±35 flip (handoff C)

In the game it occurs at every AoA (2.29.0 log: 9 episodes of 1–3 ticks in 3 min at α 0–44°; 2.27.0: 13 episodes), so it is
not a stall-plateau effect. On the same law (2.29.0, 57.6–57.8 s, α 49 → 40°) the bench's allocator holds +35 / −35 steadily
with `unmetZ` 2–15 while the game's command alternates +33 / −31 / +32 / −17 tick by tick (the surface sits at 20–28°, servo
limited). Not reproduced; the bench's state is ~5° lower in α by then (the §5 pitch item). Next: a Dev column with the
allocator's per-effector lo/hi and target on those ticks.

## 9. Acceptance block (`bench.sh replaycheck <tag> <logdir>`, cases `bench/calib/leaf_cases.json`)

On the 2.29.0 log (10 pushes): r ≤ 5, |β| ≤ 2, yaw shed ±10, body-roll peak ±10, capZeN ±10 pass on 7–9 of 10; the body pitch
rate criterion (|q_b − q_g| > 5 deg/s for > 3 ticks within 1 s) fails on 9 of 10 (§5). Other builds' logs are replayed for
information only (the current source's law on their state). **The block does not pass; no push-over law change ships on this
bench until §5 is closed.** Static check (§1): kβ within 10 %, roll and yaw parts within 20 % at 40–70°.

## 10. Files

`sim/EqErr.cs` (`LogRefRoot`, `LogRefOffset`), `sim/Plant.cs` (`DCnbHi`, `aBody`), `sim/Airframe.cs` (`Mis*Hi`, `MisHiA0/A1`,
`Mis(name, alpha)`), `sim/SlipProbe.cs` (+ `Program.cs` mode `slipprobe`), `sim/Replay.cs` (rudder / aileron / flap / TVC
command columns), `an/replaycheck.py`, `calib/fit_leaf.py`, `calib/eval_leaf.py`, `calib/leaf_cases.json`, `bench.sh`
(`replaycheck`). Suite A/B `leafcal` (DCnbHi −0.4) vs `base` (DCnbHi 0): `out/suite_leafcal_vs_base_*.diff`.
