# Revoker bench — leaf check (3 Nov 2026, from the F-22E leaf calibration)

Bench-only. Nothing in the FCS changed. Data: the 1 Nov Revoker logs 00:03 / 00:21 / 01:40 / 22:45 (0.3.0–0.4.x governor-port
flights; 59 600 airborne samples, 21 400 at |AoA| ≥ 40°). No 3 Nov-style push-over set exists for this jet. Train 01:40 / 00:21,
held out 00:03 / 22:45. Nothing here was flown.

## 1. Reference-point fix (`EqErr.LogRefRoot = 1`, same bug as the F-22E's)
The logged alpha / beta / tas are the root rigidbody's (`Fighter1`, CoM (0, −0.11, 0) — 0.95 m ahead of the CG at (0, −0.26, 0.95)),
not the CG's. Before: the canards' logged local AoA disagreed with the bench's by +0.009 deg per deg/s of pitch rate (canards ahead of
the CG, so the sign is opposite to the F-22E's stabs); after: +0.001, residual std 0.22 → 0.11 (L) / 0.18 → 0.08 (R), ailerons
0.25 → 0.12. Equation error essentially unchanged (|α| ≥ 40°: roll 12.9 / pitch 9.6 / yaw 6.6 after vs 12.4 / 10.0 / 6.1).
`LogRefRoot=0` = the old pose. The bench's own suite is unaffected.

## 2. Where the plant stands at the leaf (`calib/derivs_generic.py`, per 1e4 Pa, game | bench)
| band | L_beta | N_beta | kB |
|---|---|---|---|
| 40–50° | −6.1 \| −11.1 | 9.0 \| 7.6 | 10.7 \| 13.2 |
| 50–60° | −5.0 \| −10.3 | 9.2 \| 8.6 | 9.4 \| 13.4 |
| 60–70° | −9.0 \| −16.5 | 12.0 \| 13.4 | 12.9 \| 20.4 |
Canard / aileron / yaw-TVC derivatives within 5–15 % at 60–70° (dCan 0.97, dAil 1.16, tvcYaw 0.88 game/bench). The regression says
the bench's Revoker is 25–60 % too stiff laterally at the leaf, almost all of it in the dihedral roll (L_beta ≈ 1.8× the game's).
**Not confirmed by the equation error**: a fitted roll/yaw stiffness correction (`DClbHi`, `DCnbHi`, blended 35 → 50°) runs away to
+20 / −4 on the training logs for a 0.9 deg/s² roll gain and loses on the held-out ones (roll 10.3 → 13.1). Same caveat as the Ifrit:
closed-loop partial derivatives. The terms ship at 0.

## 3. Known weak axes (pre-existing, not leaf-specific)
Roll equation error 36.8 deg/s² at 0–20° (the Revoker's low-AoA roll was never fitted), yaw 6–9 at every AoA (the yaw TVC / rudder
model; the F-22E's is 3). A leaf push-over law cannot be judged on this bench until those are fitted.

## 4. Tools (ported from the F-22E tree)
`Airframe.Mis*Hi` + `MisHiA0/A1` (inactive by default), `Plant.DClbHi / DCnbHi` (0), `calib/fit_leaf.py`, `calib/eval_leaf.py`
(`--prefix=Revoker_FCS_`), `calib/derivs_generic.py`. No `replay` mode in this tree, so no `replaycheck` block.
