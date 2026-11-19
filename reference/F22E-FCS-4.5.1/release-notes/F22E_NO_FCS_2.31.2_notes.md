# F-22E FCS 2.31.2 — passive-airframe telemetry (drives nothing)

Base: 2.31.1 (2.31.0 law, accepted 17:43). **Compiled against the game's assemblies + bench-proven: the flight law is unchanged.
Not flown.** New CSV columns only; no player or Dev key added or changed. 2.31.1 kept as `.dll.2.31.1.bak`.

## Why (Tom, 18:20: "do 1 … only output the numbers, not to the FCS law")
Handoff 3.1 asked for the airframe's passive lateral capacity. Bench finding (`claude/Passive_lateral_stiffness_finding.md`): the
"logs kβ 6.0 / 4.0 / 5.0" target was a regression artefact; the static sideslip stiffness above 40° is small (kβ 1.0 / 0.4 / 0.04
deg/s² per deg at 45 / 55 / 65°, flight q̄), and what balances the jet is the rate coupling (yaw rate → roll, Lr 0.6–0.85, damped by
Lp −0.7 to −0.8 per deg/s). So the passive term is the airframe's own response to sideslip **and** to the body rates.

## What was added (FcsController)
- `AirframeMomentAt(vAir, ω)`: the whole airframe's aero moment, every aero part as the game's AeroJob_Math does it (the bench
  plant's per-part law): flow at the part vAir + ω × arm, lift / drag from the part's table, parasitic drag, transonic factor, the
  buffed profile's drag relief, stab downwash on the root body's flow, per-part force clamp (30 × mass × |v|), flap-camber
  increments as `CamberForce` applies them. Surfaces where their transforms are; no thrust.
- `PassiveStep` (only while the CSV log is open, not for AI): every tick the passive acceleration now and the change if the body
  rates go to this tick's (pCmd, rCmd); derivatives every 0.1 s.
- The old `SlipMomentAt` probe and its `slipStiff` consumer are untouched (they still fly as in 2.31.1).

## New CSV columns (appended after `relBrakeTd`; 310 → 326 + surfaces)
| column | meaning (deg/s²; x = roll right wing down, z = yaw nose right) |
|---|---|
| `psAX`, `psAZ` | the airframe's own roll / yaw acceleration now (surfaces as they sit, no thrust, no gyroscopic term) |
| `psDX`, `psDZ` | how that changes if the body rates go from (p, r) to (pCmd, rCmd) — what the measured term does not hold yet |
| `cePassXP/XN/ZP/ZN` | psD as capacity toward + / − roll and yaw (max(0, ±psD)) |
| `cePassSP/SN` | the same along the velocity-roll axis (psDX cos α + psDZ sin α) |
| `psLb`, `psNb` | roll / yaw per deg of sideslip at the present state |
| `psLp`, `psNp`, `psLr`, `psNr` | roll / yaw per deg/s of body roll rate p and yaw rate r |

## Validation (bench, all six 3 Nov logs 14:45–17:43, 13.8k states; `bench sliptruth`, `calib/passive_cmp.py`)
Against the plant's exact finite differences at the same logged states (medians at the flight q̄):

| AoA | kβ plant / new / old probe | Lr plant / new | Lp plant / new |
|---|---|---|---|
| 0–10 | 3.97 / 3.84 / 4.24 | 0.40 / 0.34 | −1.82 / −2.24 |
| 20–30 | 5.10 / 4.96 / 6.97 | 0.81 / 0.86 | −1.89 / −2.31 |
| 40–50 | 1.01 / 1.18 / 2.16 | 0.85 / 0.98 | −0.74 / −0.88 |
| 50–60 | 0.42 / 0.63 / 1.20 | 0.77 / 0.91 | −0.81 / −0.92 |
| 60–70 | 0.04 / 0.23 / 0.64 | 0.60 / 0.72 | −0.68 / −0.76 |

- Within ~20 % (or 0.2 deg/s² per deg where the value is near zero) on β and rate derivatives at 0–70°; Lp at 0–40° reads 20–23 %
  strong and the negative-AoA rate terms 25–55 %. The old probe was 2–15× off above 40°.
- ~11–12 points of the excess is the bench's effective-inertia calibration (`IScaleRoll/Yaw` 1.12 / 1.13 on the plant, not on the
  FCS's own inertia); with the plant's bench-only multipliers off (Mis* = 1, DCnbHi = 0) the evaluator matches it to ~5 % after that
  factor. In the game the FCS uses the real rigidbody tensors, so this offset is a bench artefact; the flight log will show which is right.
- Present-state passive acceleration vs the plant (multipliers off): median error 1–9 deg/s² against a spread of 20–220.
- Per-part tables are identical to the plant's (`bench partcmp`); the remaining full-plant gap is the bench's game-vs-table
  multipliers (stabs ×0.72, rudders ×1.18, wing/body ×0.95, DCnbHi) — the same thing the capacity estimator's group gains carry.
  In flight, `psAX/psAZ` against the measured `ceWdX/ceWdZ` (less TVC) will measure that gap directly.

## Proof the law is unchanged
- Full suite (rev / cork / reg / util / dmg): all five summaries identical to 2.31.1 apart from the tag name; **all 119 per-tick
  case CSVs byte-identical**. The bench's telemetry stub is non-null, so `PassiveStep` ran on every tick of every case.
- A scripted rolling pull with `PassiveProbeOn` true / false: byte-identical case CSV; cost +0.26 ms per tick on the 2-core
  container (with the suite running beside it), only while logging.
- Build environment checked first: rebuilding the untouched 2.31.1 here gives the installed DLL byte for byte (md5 `cff93e89…`).

## Bench additions
`bench sliptruth` (plant vs probe vs new evaluator, per state), `bench partcmp` (per-part table check at one logged state),
`calib/passive_cmp.py`. `CALIBRATION_leaf.md` §6 corrected (its "logs kβ" column is a regression coefficient).

## What to look at in the first log
- `psLb psNb psLr psLp` against the bench numbers above (same AoA bands);
- `psAX/psAZ` vs `ceWdX/ceWdZ` in steady flight (the gap = TVC + the game-vs-table factor);
- `cePassSP/SN` in corkscrews and the falling leaf: how much of the rotation the airframe would carry by itself, before the measured
  term sees it.
