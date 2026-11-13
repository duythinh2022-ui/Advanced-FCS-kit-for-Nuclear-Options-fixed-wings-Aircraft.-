# Su-30VXN bench calibration against the 9 Nov logs (9 Nov 2026)

Bench only: no FCS behaviour or tuning value changed. Nothing here has been flown.

## Data
`Su30_FCS_20261109_{090528,095915,102540}.csv` (0.1.7 / 0.1.8), mode 3, TAS > 40 m/s: 35,379 samples, 19,356 of them above 40 deg AoA, up to about 540 km/h.
The 8 Nov logs were not used to fit: they ran older airframe settings, and the bench has one config.

## Bugs found (these come first; they are not fits)
1. **`eqerr` / `replay` posed no control surface on the Su-30.** `EqErr.Poser` matched log names (`elevator_L`) against blueprint names (`Left_stab_struct`), so all eight lookups failed. Warnings went to stderr. Every equation-error figure for this jet before today was the airframe with all surfaces at zero, including the table in `Su30_bench_handoff_2026-11-09.md`. Fixed with `Names.Canon`.
2. **`Plant.BuildCamber` found no camber host on the Su-30**, for the same naming reason. Flap and fin camber coupling was missing from every closed-loop Su-30 bench run. The game logs "6 surfaces paired". Fixed.
3. **Canted nozzles in `eqerr`** came from the FCS's internal state, not the log. Now posed from `nozL` / `nozR`.
4. **`LocalAoA` diagnostic** applied the stab downwash to the canards (kind = Stabilator). Fixed to the plant's own test. Diagnostic only.
5. **`DragScale` (0.9)** was not applied on the Su-30 path; the game logs "35 parts drag x0.9". Fixed.
6. **Mass:** `eqerr` now uses the logged `massKg` per sample (inertia scaled with it, CG not moved).
7. **Canard hinge in `eqerr`:** solved from the logged `canard_*_aoaNow`, so logs with the stock schedule still running (before 0.1.8) pose correctly.

## Calibrated factors (bench-only; `IfritTest.Defaults`, su30 branch)
| factor | value | how it was found |
|---|---|---|
| `Airframe.FlapColInnerZ`, `FlapColOuterZ` | 0 | After the fixes the pitch error was a clean function of dynamic pressure. Regressed on per-group normal forces, the flaps' force carried a coefficient of -1.3 m in every leave-one-log-out fit (-1.28 to -1.36), against about -0.1 m for the wing. The prefab's flap `centerOfLift.z` is 1.3 m. The x component (+-1.5 m, outer flaps) stays: zeroing it made roll worse. |
| `Plant.ThrustLineDy` | -0.115 m | Constant +30 kN m nose-up left over, about 4.4 deg/s^2 at every AoA. Thrust is near maximum in almost all samples, so this cannot be told apart from any other constant moment. |

**Cause of the flap offset is unknown.** The game's `AeroJob_Math` does apply `Cross(force, -(rot * centerOfLift))` (read from the IL), the same as the plant. So the 1.3 m is either not what the live part holds, or something else cancels it. Logging each flap's `centerOfLift` and `rb.worldCenterOfMass` at spawn would settle it. It also matters for the FCS: its own effector models use `centerOfLift` at run time.

## Equation-error RMS, 9 Nov logs (bench minus game, deg/s^2)
| | roll | pitch | yaw |
|---|---|---|---|
| spread of the game's accelerations, all samples | 50.1 | 33.4 | 17.4 |
| surfaces posed, before the factors | 5.9 | 21.9 (bias +12.5) | 2.1 |
| **calibrated** | **5.7** | **6.2 (bias +0.4)** | **2.2** |
| spread above 40 deg AoA | 15.2 | 28.1 | 18.4 |
| surfaces posed, before the factors, above 40 deg | 2.9 | 12.5 (bias +9.0) | 2.4 |
| **calibrated, above 40 deg** | **2.8** | **2.4 (bias -0.1)** | **2.5** |

Per log above 40 deg AoA, pitch RMS / bias: 090528 2.6 / +0.9; 095915 2.5 / -0.6; 102540 2.1 / +0.5.

## The handoff's table, re-run (102540 log)
| t (s) | AoA | Stabs L / R | Bench q-dot | Game q-dot | Bench p-dot | Game p-dot |
|---|---|---|---|---|---|---|
| 196.6 | 58 | 35 / 35 | -66 | -65 | -5 | -4 |
| 196.9 | 63 | 31 / 31 | -58 | -53 | 1 | 3 |
| 197.2 | 65 | 1 / 11 | -77 | -75 | -17 | -15 |
| 197.6 | 59 | -30 / 5 | -14 | -11 | -29 | -27 |
| 198.2 | 46 | 18 / 35 | 81 | 86 | 44 | 48 |
| 198.6 | 47 | 35 / 9 | 21 | 21 | -72 | -78 |

## Closed-loop replays (logged stick, 0.1.8 settings)
RMS against the game; `flap offset kept` = all bug fixes but the two factors off.

| window | AoA, flap offset kept | AoA, calibrated | Peak AoA game / calibrated |
|---|---|---|---|
| 095915, 89-94 s | 8.2 | 5.6 | 65 / 66 |
| 095915, 155-160.5 s | 3.8 | 3.1 | 71 / 72 |
| 095915, 194-198.5 s | 5.0 | 2.0 | 67 / 70 |
| 095915, 386-391.5 s | 4.4 | **5.2** (worse) | 72 / 67 |
| 102540, 43-49 s | 2.6 | 1.2 | 75 / 73 |
| 102540, 119-124.5 s | 3.5 | **3.9** (worse) | 71 / 68 |
| 102540, 193.8-200.5 s (handoff check) | 8.4 | 4.8 | 69 / 69 |

Handoff pass test, 193.8-200.5 s: the game's AoA falls to 45.7 deg at 198.2 s. The calibrated bench reaches 44.7 at 197.8 s and 45.5 at 198.2 s, so about 0.4 s early on the way down. Before the calibration it stayed at 64-66 deg.
These replay windows come from the same three logs the factors were fitted on, so they check the closed loop but are not independent data.

## Still wrong or unknown
- **15-40 deg AoA above about 550 km/h: the bench is 20-50 deg/s^2 too nose-up** (about 500 samples at up to 8.5 g). Below 450 km/h it is within about 5. Cause not found. Full-aft pull onsets from high speed run through this region, so judge them with care.
- Roll and yaw errors grow with speed above 40 deg AoA: at 450-600 km/h roll bias -6.2 and yaw bias -3.8 (349 samples).
- The closed loop tends to peak 2-5 deg low in two of the seven windows.
- Rigid-body plant: the game's jet is 34 jointed bodies.

## Tools (`/bench/calib` style, kept with this note under `scripts/su30/calib/`)
- `eqrun.sh`, `eq9.sh`: run `eqerr` over the logs. `eq9.sh` adds `groups=1` (per-group moments and normal forces, columns `M_*`, `F_*`).
- `score.py <tag>`: RMS / bias per AoA band and per log.
- `wingfit.py <tag>`, `grp.py <tag>`: regressions of the pitch error on group forces, with leave-one-log-out.
- `rpval.sh <tag> [Name=value]`, `rpsum.py <tags...>`, `rpcmp.py`: closed-loop replay windows and comparison with the game.
- Pre-0.1.8 logs: use them for pose checks only; their airframe settings differ.
