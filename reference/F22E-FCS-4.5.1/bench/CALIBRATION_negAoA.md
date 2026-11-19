# F-22E bench: negative-AoA calibration (31 Oct 2026, on the 2.5.4 source)

Bench-only. No FCS behaviour change, no tuning value touched, nothing flown. Follows `CALIBRATION.md` (the 31 Oct positive-side calibration, 2.4.18) and `claude/F22E_bench_negAoA_calibration_handoff.md`.

## The short version
1. **The 2–3× sideslip over-read of the negative-AoA replays was the replay, not the plant.** The replay settled the FCS on a frozen plant for 1 s, which left its own state far from the game's at t0: slats at 0 (game 0.87), pitch trajectory `qT` −41 (game −15), roll reference 52 (game 101). The allocator spent the first 0.3 s of every window answering that (stab −21° vs the game's −6° for the same pitch command) and the roll/pitch/yaw states had diverged before the AoA crossed zero. `bench replay` now warm-starts the FCS from the log (`warm=1` default: slat position, `qT` and its rate, the velocity-vector roll trajectory from `pCmd`/cos α and its rate, `gInt`, `aInt`). With the 2.5.3 FCS that flew the 09:44 log: mean peak-sideslip error over the handoff's 11 windows 4.66 → 2.31°, β RMS 3.56 → 2.09, α RMS 6.0 → 3.6. 82 s 14.9 → 7.1 (game 7.7), 312 s 18.6 → 8.8 (8.6), 334 s 14.0 → 9.4 (6.6), 249 s 14.9 → 11.6 (8.7).
2. **One plant bug found and fixed in code:** in `Plant.Forces` the flap/aileron/rudder camber coupling recomputed the host part's lift *without* the bench's `Mis*` multiplier, so `MisWing` never reached `Wingmid`/`WingRear` while a flaperon was deflected and `MisFin` never reached the fins while a rudder was (which is why `MisFin` had no measurable effect at first). Fixed (`Plant.CamberMis`, default on; `false` = the 31 Oct bench). Effect on the calibrated numbers: 30 Oct logs roll/pitch/yaw/nz 14.3/20.2/3.51/0.38 → 14.2/20.2/3.45/0.37; the factors keep their names and values.
3. **The negative side of the plant is as good as the positive side.** Equation error over the 42 s of AoA < −5 in the four 31 Oct logs: roll 8.9, pitch 15.9, yaw 3.2 deg/s², nz 0.36 g (positive side of the same logs 14.4 / 20.1 / 3.6 / 0.36; the accelerations' own spread is smaller on the negative side, see the table). Per-AoA-band derivatives: Clβ has the right sign everywhere and is 15–18 % too strong at −20…−5° (0.97–0.98 of the game at −5…0 and on the positive side); Cnβ within 11 % from −30° to +15°; roll control (differential stab, ailerons, flaperons) 0.9–1.2; nz within 0.15 g. **No new factor was fitted**: what the data left (below) could not be improved by any single named factor without making the positive side or the closed loop worse. The airframe json is unchanged (`F22E_bench_airframe_2.4.18-cal.json` stays the one to use).
4. **The handoff's hypothesis — "a large pro-roll moment at negative AoA with sideslip" in the bench airframe — is not supported.** In the bench cases where the roll kept accelerating after the roll target was cut (`reg.py` c14/c16, and the 312 s replay) the stabs were still deflected pro-roll because the FCS's roll *trajectory* (`pCmd`, RateTraj) was still 60–90 deg/s while the *target* (`pSTgt`) had gone to 0, and the stabs were at their pitch travel for the −55 deg/s push. The plant's roll bias in those windows is +6 deg/s² (equation error), not the 100–200 the cases show.

## Data
`F22E_FCS_20261031_{094454,014032,011100,005033}.csv` (2.5.1–2.5.3, current game build, Buffed, gusts on): 63,711 mode-3 airborne samples, of which 2,526 (42 s) at AoA < −5° and 1,208 (20 s) at AoA < −20°. 09:44 alone: 26 s below −5°, 21.5 s below −15°. Equation error as in `CALIBRATION.md` (`bench eqerr`, SG derivatives, `calib/eqcmp.py`, `derivs.py`), plus two new tools: `bench parts alpha= beta= tas=` (per-part force / moment breakdown at a posed state), `eqerr ... dumpAt=<t>` (the same at a logged sample), `calib/axcheck.py` (axial acceleration — dTAS/dt + g sin γ — against the bench's thrust − drag, by AoA band), `calib/dterms.py` (reads `derivs.py` tables).

## Equation error by AoA band, 31 Oct logs (predicted − measured; RMS deg/s², nz g; `sd` = spread of the measured value)
| AoA | n | roll RMS (sd) | pitch RMS, bias (sd) | yaw RMS (sd) | nz RMS, bias |
|---|---|---|---|---|---|
| −60…−30 | 812 | 10.6 (11.4) | 12.0, +7.3 (35) | 3.60 (10.1) | 0.32, −0.15 |
| −30…−20 | 396 | 8.9 (28) | 16.4, +4.9 (42) | 3.97 (10.0) | 0.42, +0.13 |
| −20…−10 | 821 | 6.6 (55) | 16.6, +2.4 (51) | 2.59 (11.8) | 0.32, +0.04 |
| −10…−5 | 498 | 9.4 (122) | 19.3, −2.3 (55) | 2.94 (15.7) | 0.44, −0.06 |
| −5…0 | 1639 | 12.8 (129) | 15.1, +3.6 (30) | 2.15 (12.2) | 0.36, −0.18 |
| 0…5 | 6091 | 16.8 (175) | 18.9, +3.0 (36) | 2.39 (11.0) | 0.32, −0.13 |
| 5…15 | 13122 | 14.6 (143) | 19.9, −4.2 (43) | 3.01 (19.7) | 0.34, −0.06 |
| 15…30 | 15511 | 18.5 (88) | 26.5, +1.5 (37) | 4.56 (27.4) | 0.37, −0.02 |
| 30…45 | 7809 | 11.1 (45) | 21.5, −5.6 (42) | 4.04 (24.6) | 0.40, +0.08 |
| 45…90 | 17010 | 9.6 (19) | 11.9, −2.6 (31) | 3.08 (18.0) | 0.36, +0.13 |

(after the camber fix; before it the rows differ by ≤ 1 deg/s² — roll 0–15° 14.9/12.8 → 16.8/14.6, 15–45° 19.7/11.6 → 18.5/11.1, pitch 15–30° 27.5 → 26.5.) The 162–168 s clean push (−45.7°, 165 deg/s of roll, β < 2.3): roll RMS 6.6 against a spread of 116, pitch 10.7 (bias +6.5), yaw 1.5. Inside the seven handoff windows the roll RMS is 7–14 (bias −3…+9), pitch 11–19, yaw 1.5–7.

## Derivatives at negative AoA (game | bench per AoA band, `derivs.py`, 31 Oct logs; ratio = game / bench)
| band | Clβ (roll per deg β) | Cnβ (yaw per deg β) | roll: dStab / dAil / dFlap | yaw: rudder | pitch: sym. stab | roll damping p |
|---|---|---|---|---|---|---|
| −55…−30 | −1.65 \| −0.72 (2.3)* | 2.64 \| 3.82 (0.69) | 1.01 / 0.43* / 0.13* | 3.1* | 1.01 | −0.21* |
| −30…−20 | −0.30 \| −0.72 (0.42)* | 1.90 \| 1.94 (0.98) | 1.26 / 1.14 / 1.96 | 0.65 | 1.64 | 0.94 |
| −20…−10 | −10.0 \| −12.2 (0.82) | 5.25 \| 5.89 (0.89) | 0.91 / 0.77 / 1.21 | 0.82 | 1.40 | 1.04 |
| −10…−5 | −15.5 \| −18.2 (0.85) | 7.49 \| 7.61 (0.98) | 0.96 / 1.17 / 0.93 | 0.88 | 1.34 | 1.00 |
| −5…0 | −19.2 \| −19.8 (0.97) | 6.63 \| 5.91 (1.12) | 1.00 / 1.31 / 0.92 | 0.51* | 1.21 | 1.06 |
| 0…5 | −9.0 \| −9.4 (0.96) | 2.66 \| 2.44 (1.09) | 0.95 / 1.11 / 1.05 | 3.0* | 1.55 | 1.04 |
| 5…15 | −7.8 \| −8.3 (0.94) | 2.74 \| 2.74 (1.00) | 0.96 / 1.05 / 1.07 | 0.60* | 1.90 | 0.98 |
| 45…90 | −5.4 \| −5.8 (0.94) | 0.94 \| 1.47 (0.64) | | | | |

\* regressor nearly absent or collinear in that band (R² < 0.5 for that term) — not a measurement. Clβ keeps the dihedral sign through zero AoA in the game and in the bench (it comes from the canted fins + rudders above the CG, not from the wing — `bench parts alpha=-15 beta=5`: fins + rudders −23.5 deg/s² of the −21.4 total, wings ≈ 0), peaks at −5…0 and falls off both ways; the game's falls off ~15 % faster on the negative side. **Cnβ is 45 % too strong below −30° and 36–55 % too strong above 45°** — symmetric in |AoA|, where the canted fins see 0.5·|α| ± 0.87·β of local flow and are past their stall; the bench and the game use the same chart and the same per-part math for them, so the cause is not in the fin model itself; unresolved (candidates: the game's part velocities on the jointed assembly at those rates, the force clamp on the 50 kg rudders).

What was tried and not adopted (all bench-only, all reverted):
- `MisFin` 0.8 / 0.7 / 0.5: brings the negative-side Clβ to 0.92–0.94 and Cnβ (−20…−10) to 1.00 but pushes the positive side to 1.05–1.12 / 1.14–1.33, raises the 31 Oct roll RMS 14.2 → 16.4 → 18.7, and makes the warm replays worse (328 s 18 → 20, 334 s 9 → 16). The fins are right or slightly weak for the closed loop.
- `MisRud` 1.0 (instead of 1.18): the same trade in the other words — negative-side Clβ 0.96, Cnβ 1.03, rudder 0.92, but roll RMS 14.2 → 17.3 overall. `MisFin` 1.35 with `MisRud` 1.0 scores the same as 1.0/1.18 (14.07 vs 14.18): fin and rudder are not separable in this data (both are 3 m² canted panels at the same station; the rudder regressor is collinear with the roll stops on the positive side).
- **`MisStabPitch`** (new, opt-in): the per-axis stab split the 2.4.18 report suggested — `MisStab` 0.72 on the differential (roll) half of the two stab forces, `MisStabPitch` on the symmetric (pitch) half. The per-band regressions put the game's symmetric-stab pitch term at 1.2–1.9× the bench's, so 1.0 was tried: pitch RMS 20.2 → 35.9, bias −25 deg/s², scale 0.54 on the 30 Oct logs. The regression coefficient is not a control power (stab deflection, AoA and nozzle angle are collinear in trimmed flight); the dynamic transfer function in `CALIBRATION.md` (|H| 1.02–1.05) and the total equation error both say the stab pitch power is right as calibrated. Field kept at −1 (= no split) as a tool.
- Adopted: nothing beyond the camber fix.

Other things measured on the negative side: pitch bias +5…+7 deg/s² nose-up below −20° (the same sign as the +3 on most positive bands); nz within 0.15 g (lift curve right on the negative side); **axial**: `axcheck.py` on 09:44 — thrust − drag within 0.2 m/s² of the game from −5 to +20° AoA, bench 0.5–1.0 m/s² *less* deceleration at −30…−5 (opposite sign to the rest), 1.0 m/s² *more* below −30 and 1.5–2.3 m/s² more above 45° (bench drag ~30 % high post-stall; the drag relief is modelled identically on both sides — `DragRelief.FixedUpdate` vs `Plant.Forces` — so it is not that; unresolved, it is why the pull-through replays end 10–17 km/h slow).

## Closed-loop replays (pilot's sticks, peak |β| bench vs game, the handoff's 11 windows)
| window | game | 31 Oct bench, cold start (2.5.3) | camber fix, cold | **camber fix, warm (2.5.3, the FCS that flew)** | warm, 2.5.4 |
|---|---|---|---|---|---|
| 09:44 82 s | 7.7 | 14.9 | 14.8 | **7.1** | 7.1 |
| 249 push-over | 8.7 | 14.9 | 12.5 | 11.6 | 10.5 |
| 251–253 | 10.8 | 5.6 | 6.6 | 12.4 | 7.3 |
| 280 pull-through | 13.5 | 6.1 | 6.1 | 8.7 | 7.0 |
| 312 push-over | 8.5 | 18.6 | 18.8 | **8.8** | 8.7 |
| 317 pull-through | 15.2 | 15.0 | 15.1 | 15.3 | 17.8 |
| 324 push-over | 9.3 | 7.3 | 7.1 | 5.9 | 7.6 |
| 328 pull-through | 10.8 | 15.1 | 14.7 | 18.1 | 18.2 |
| 334 push-over | 6.6 | 14.0 | 14.0 | 9.4 | 7.7 |
| 01:40 24–27 s | 6.4 | 5.5 | 5.4 | 5.5 | 5.5 |
| 01:40 191–194.5 s | 1.6 | 2.0 | 2.1 | 2.1 | 2.1 |
| mean peak error | | 4.66 | 4.37 | **2.31** | 2.44 |
| β RMS / α RMS | | 3.56 / 6.0 | 3.32 / 5.6 | 2.09 / 3.6 | 2.12 / 5.6 |

Six of eleven are now within 1.3×, three over-read (249 s 1.33×, 334 s 1.4×, 328 s 1.7×) and two under-read (280 s and 324 s, 0.64×); the ones that miss diverge from the first ticks in the FCS's *commands* with the states still equal (280 s: bench stab +25° vs the game's +1.5° at 279.1 s for the same stick, then the game's roll reference went to −66 deg/s while the bench's governor held 0) — FCS state the log does not carry (`rS`, the governor holds, `capOn`/`capQ`, the filters), not plant. The 2.5.4 column is not FCS-matched to a 2.5.3 flight and is there for the direction only.

## `reg.py` / `sweep.py` with 2.5.4 on the recalibrated plant (bench-only, camber fix; the 31 Oct plant in brackets where it differs by > 0.5°)
Level 3 (default config), peak |β| in the case window: pull-then-roll 250/320/400: 3.4 / 5.1 (2.9) / 3.9 (7.8); fullaft+roll 250: 2.3; fullaft+roll+rev 320/500: 3.3 / 3.6; roll-then-pull-rev 400: 3.5; held 0.4 + roll 350: 0.8; 65° release 300 (with / without yaw): 3.5 / 3.5; push+roll 200/300: 3.7 / **7.6 (9.5)**; pull-then-pushover+roll 350/450: 3.3 / 3.6; **push-then-pullthrough+roll 350: 40.7 (40.9)**, 450: 8.1 (9.1); pullthrough, roll released at zero 350: **40.7**; 1 g roll rev 500: 2.1. Level 1 (`RollGovernorReplaces=1`): pull-then-roll 5.2 / 3.5 / 6.7, fullaft+roll 5.0, rev 6.8 / 5.6, roll-then-pull-rev 4.4, push+roll 200/300 3.4 / 14.8, pushover+roll 6.4 / 3.5, **pullthrough+roll 350: 22.3**, 450: 11.0, released at zero: 22.3, 1 g rev 2.1. Stop/reversal sweep (32 cases, 450–850 km/h): L3 worst stop / reversal 4.1 / 4.1, mean 2.31 / 2.74, mean roll 112 deg/s (2.5.4 notes on the 31 Oct plant: 3.2 / 3.9, 2.1 / 2.6, 108); L1 4.6 / 5.1, 2.39 / 2.99.

**What the bench says about the 40° case** (push-then-pullthrough+roll 350, L3; the plant is now within 15–20 % of the game on every lateral term that matters here, so this is worth reading as a prediction with that error bar, not a plant artefact): full push from 1 g trim at 350 km/h asks the pitch loop for −50…−57 deg/s; the stabs go to −30° (where the pitch loop holds them) and have no roll authority left. Full roll from 0.35 s: the roll target reaches 135 deg/s, the yaw demand to coordinate it at negative AoA (rCmd −15 → −30 deg/s) puts both rudders on their stops by 1.0 s (β −3°), the yaw rate stays at +2 deg/s, and the pace governor cuts the roll target to 0 at 0.95 s (α −10°). The roll *trajectory* then brakes at only ~70 deg/s² (82 → 57 over 0.4 s) and the body roll keeps rising to 113 deg/s until 1.2 s because the only anti-roll effector left is the flaperons (the stabs are at their pitch travel and their differential is countering the rudders' own rolling moment — a canted rudder's roll moment is 0.7× its yaw moment, on a third of the inertia, so it makes twice the roll acceleration it makes yaw, `bench parts`); β then grows at p·sin α ≈ 45 deg/s to −40° by 2.3 s. In the game's closest event (312 s: the same sticks from α +17 at 385 km/h) the pitch demand at the crossing was −25…−35 deg/s, the stabs were at ±15° with margin and braked the roll at −155 deg/s² together with the flaperons, and β peaked at 8.5 — the warm replay reproduces that at 8.8. So the difference between the two is the pitch demand at the crossing, not the plant: the bench case is a harder input than anything in the logs. If the governor is to protect it, the lever the bench points at is the roll-trajectory braking rate when the yaw is pinned at negative AoA (it brakes at the yaw's pace, which is ~0 here) and the pitch-rate cap during the sweep when the stabs are at their travel; a direct game test of this input (full push from level flight, then full roll, 350 km/h, 1.5 s) would settle how much of the 40° is real. Not an FCS change in this task.

## Files (bench only; 2.5.4 FCS source unchanged)
- `sim/Plant.cs`: `CamberMis` (the fix), `MisStabPitch` split (opt-in), `PartDump`.
- `sim/Airframe.cs`: `MisStabPitch` static (−1 = off).
- `sim/Replay.cs`: `warm=` (default 1) — `WarmState()`; `sim/EqErr.cs`: `parts` mode, `dumpAt=`; `sim/Program.cs`: the new keys.
- `calib/axcheck.py`, `calib/dterms.py`, `calib/neg_cases.json` (the 11 windows); `an/reg.py`, `an/sweep.py` take `AN_OUT`, `BENCH_DIR`, `BENCH_DATA` from the environment instead of a scratchpad path.
- `regen.sh` reads `../tomcfg.cfg` (the two config files concatenated, next to the source).

## For the next FCS chat (from the bench, with the caveats above)
- 2.5.4's moderate-AoA stops/reversals on this plant: L3 worst 4.1 / mean 2.3–2.7 at 112 deg/s; L1 4.6–5.1 / 2.4–3.0. Negative-AoA crossings in the logs replay within 1.3× for 8 of 11 windows once the FCS state is right.
- The one thing that departs on the bench is full push from trim + full roll (pitch demand −55 deg/s with the stabs at their travel and the rudders pinned): 40° at L3, 22° at L1. More data is the cheap next step: a flight of pushes to −20/−35/−50° at 250/350/450 km/h with and without roll, and one full-push-from-level + full-roll at 350 km/h.
- Telemetry that would make the replays FCS-matched instead of state-guessed: log `pS` (the velocity-vector roll trajectory; `rS`, `qT`, `paceHold`, `stopSlipHold`, `capOn` are already there), the trajectories' rates (`pTraj.v`, `qTraj.v`) and the stick filters' states — all floats already in the FCS; `Replay.WarmState` would then set them instead of deriving `pS` from `pCmd`/cos α.
