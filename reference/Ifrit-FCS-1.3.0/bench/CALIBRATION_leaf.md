# Ifrit bench — leaf check (3 Nov 2026, from the F-22E leaf calibration)

Bench-only. Nothing in the FCS changed. Data: the 1 Nov Ifrit logs 00:12 / 01:18 / 01:48 / 22:48 (0.7.x governor-port flights;
37 800 airborne samples, 14 000 at |AoA| ≥ 40°). No 3 Nov-style push-over set exists for this jet. Train 01:18 / 00:12, held out
01:48 / 22:48. Nothing here was flown.

## 1. Reference-point fix (`EqErr.LogRefRoot = 1`, same bug as the F-22E's)
The logged alpha / beta / tas are the root rigidbody's (`Multirole1`, CoM 0.62 m ahead of the CG), not the CG's. Before the fix the
ruddervators' logged local AoA disagreed with the bench's by −0.006 / +0.006 deg per deg/s of yaw rate and −0.007 / −0.005 per deg/s of
pitch rate (= 0.62 m / TAS); after: ≤ 0.002, residual std 0.23 → 0.14 (L) / 0.28 → 0.18 (R). At the leaf (60 deg/s, 78 m/s) the
old pose put ~0.5° of spurious sideslip into every replay's start and into the FCS's β read in the bench (the F-22E's arm is 2.48 m,
so its error was 3×). Equation error is unchanged by it (roll 13.8 / pitch 20.1 / yaw 4.1 at |α| ≥ 40° after; 13.7 / 20.2 / 3.8 before).
`LogRefRoot=0` = the old pose. The bench's own suite is unaffected (the FCS reads the root body in game and bench alike).

## 2. Where the plant stands at the leaf (`calib/derivs_generic.py` on `eqerr`, per 1e4 Pa, game | bench)
| band | roll damping Lp | aileron roll | flaperon roll | L_beta | N_beta | kB |
|---|---|---|---|---|---|---|
| 40–50° | −0.44 \| −1.12 | 1.76 \| 2.74 | 1.42 \| 1.23 | +0.37 \| +0.59 | 5.35 \| 5.61 | 3.55 \| 3.59 |
| 50–60° | −1.67 \| −2.96 | 1.63 \| 2.70 | 1.25 \| 0.81 | −1.27 \| −0.02 | 2.69 \| 3.78 | 2.58 \| 2.18 |
| 60–70° | −1.58 \| −3.34 | 1.10 \| 2.73 | 0.55 \| 0.04 | −2.83 \| **+5.28** | 3.17 \| 5.53 | 3.92 \| **−2.37** |
Yaw: ruddervator yaw and roll-TVC yaw within 10 % at 40–70°; yaw damping 0.5–0.7 of the game's at 50–70°.
The 60–70° row says the bench's Ifrit is laterally *unstable* in the stability axis where the game's is stiff, with the bench's
ailerons and roll damping 2–2.5× the game's. **The equation-error objective does not confirm the regression**: halving the
ailerons' high-AoA lift (`MisAilHi=0.45`) makes the roll error worse (12.6 → 16.0 at |α| ≥ 50°), and a fitted roll/yaw stiffness
correction (`DClbHi`, `DCnbHi`, blended 45 → 60°) goes to +2.5 / −1.5 on the training logs and loses on the held-out ones
(roll 11.07 → 11.29). The per-band coefficients are closed-loop partial derivatives — the ailerons are commanded against the
sideslip, so the aileron power error and L_beta trade against each other — and the stock wing chart (CL 1.0 at 45°, 0 at 90°,
Hermite) does put the outer panels on a negative slope at 60–70° local AoA. Not resolved; the terms ship at 0. Next: a steady-leaf
log with the ailerons parked (rudder-only or TVC-only lateral control) to break the correlation.

Also pre-existing: the decelerons' logged local AoA (`flap_*_aoaNow`) sits 10–18° from the bench's at every AoA (the split
decelerons' local AoA is defined differently from a plain flap's; the eqerr drive solve does not reach it) — a plant input error on
the inner trailing edge the equation error carries at every AoA.

## 3. Pitch (the real Ifrit item)
The plant predicts +11 to +20 deg/s² more nose-up than the game at 40–70° (bias +19.6 at 40–55°, +10 at 55–70°) — and +12.6 below
35°: a global nose-up bias, known since the Ifrit's first calibration (no collider hulls — box inertia — and the CG not known to
0.1 m). A high-AoA fuselage-lift multiplier (`MisBodyHi` 0.2–0.5) moves it by only 1–3 deg/s². Not a leaf item; the Ifrit needs its
hull mass properties and a CG fit before a leaf push-over can be trusted on it.

## 4. Tools (ported from the F-22E tree)
`Airframe.Mis*Hi` + `MisHiA0/A1` (AoA-blended bench-only multipliers, inactive by default), `Plant.DClbHi / DCnbHi` (0),
`calib/fit_leaf.py`, `calib/eval_leaf.py` (`--prefix=Ifrit_FCS_`), `calib/derivs_generic.py`. The F-22E's `replaycheck` block needs the
`replay` mode, which this tree does not have.
