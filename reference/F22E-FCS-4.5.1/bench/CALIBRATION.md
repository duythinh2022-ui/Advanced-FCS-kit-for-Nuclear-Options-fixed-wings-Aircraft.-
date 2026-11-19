# F-22E bench calibration against the 30 Oct flight logs (31 Oct 2026)

Bench-only. No FCS behaviour change, no tuning value touched. Everything below is from the bench and the logs — nothing here was flown.

## Data
Logs `F22E_FCS_20261030_{102806,143103,143501,223639,225915,232344,232741}.csv` (2.4.16–2.4.18, current game build, Buffed profile). Mode 3 only, airborne, TAS > 40 m/s: 91,987 samples at 60 Hz. Training: 102806, 143501, 223639, 232741 (67k samples). Held-out validation: 143103, 225915, 232344 (25k).

## Method
- `bench eqerr log=<csv> out=<csv>`: the plant is posed at every logged sample (TAS, AoA, sideslip, p/q/r, rho, attitude), every surface at its logged `_now`, the flaperon drives solved so each surface's local AoA matches its logged `_aoaNow`, nozzles at `tvcNow`, slats at `hlPos`, airbrake at `abOpen`, thrust = `tw` × m × g. `Plant.Forces` (the game's AeroJob_Math + the FCS hooks, no FCS, no servos, no integration) gives F and M → predicted p/q/r accelerations (I⁻¹(M − ω×Iω)) and nz. The logged rates are differentiated with a Savitzky–Golay filter (9 samples, cubic); 9 crash/respawn spike samples are dropped. Pose check: the stabs' and rudders' local AoA computed by the bench matches the game's logged `_aoaNow` to ≤ 0.06°, the solved flap drive matches the FCS formula to 0.01°.
- `calib/eqcmp.py`, `derivs.py`: lag scan, RMS per axis, error maps, per-AoA-band control derivatives (game vs bench), transfer functions.
- `calib/fit.py`: Nelder–Mead on the training logs; `evalset.py` scores any parameter set.
- `bench replay log= t0= t1= out=`: closed loop from the logged state (plant posed, FCS settled on it 1 s, logged sp/sr/sy fed as the FCS's own filtered sticks, throttle solved from `tw` through the engine model). `calib/replay_eval.py` runs the handoff's cases.

## Equation-error RMS (predicted − measured; deg/s², nz in g)
| | roll | pitch | yaw | nz |
|---|---|---|---|---|
| spread of the measured accelerations | 95.5 | 38.8 | 22.5 | 2.28 |
| **original bench** (all 7 logs) | 35.4 | 37.0 (bias +20.6) | 5.37 | 0.48 |
| after the code fixes only (all 7) | 48 | 27.3 | 5.2 | 0.45 |
| **calibrated** (all 7) | **14.3** | **20.2** (bias −1.6) | **3.51** | **0.38** |
| original, held-out logs | 27.1 | 26.0 | 4.75 | 0.45 |
| calibrated, held-out logs | 11.9 | 16.3 | 3.07 | 0.36 |

Dynamic transfer function measured/predicted (before any fit, original mass): pitch |H| = 1.02–1.05 from 0.5 to 8 Hz with coherence 0.95–0.97, yaw 0.96–1.0, roll a flat 0.86–0.88; phase lag 5–10 ms on every axis (half a tick). So the stab / TVC control power and the yaw airframe terms are right dynamically; the pitch error was a slow (< 0.5 Hz) static moment, and the roll error a flat gain. There is **no 0.3 s airframe lag**: if the yaw loop lags in game, it is in the FCS/servo path, not the plant.

## Root causes fixed in code
1. **Physics tick.** The FCS log steps 16.67 ms (`time += Time.fixedDeltaTime`): the game runs 60 Hz. The bench ran 0.02 s. `Plant.dt` is now 1/60 (also changes the FCS's own filters in the bench, as in game).
2. **Mass.** From the logged T/W at full afterburner against the game's own thrust model (`Turbofan.FixedUpdate` / `JetNozzle.Thrust`, read from the IL: thrust = (static·dyn − loss)·spool + AB·clamp(ρ,0.4,1); no parasitic loss on this jet): the jet weighed ~28.5 t at spawn and 27.2–27.8 t in these flights (structure 19,600 + fuel + ~700 kg stores, burning ~3 kg/s). The bench filled the tanks and ballasted to 29,230 kg. Default now `MassTarget=27500`, `FuelFrac=0.88`.
3. **Collider mass properties.** Unity derives each part's COM and inertia from its colliders; for a convex MeshCollider PhysX uses the hull, the bench used the mesh AABB box (Vstab box 12.2 m³ vs hull 0.69 m³; wing-panel centroids 0.3–0.4 m off). `extract/extract_hulls.py` adds the hull volume / centroid / inertia of all 39 mesh colliders; `Airframe.BuildBodies` already preferred them. Inertia −9…−11 % on all axes, CG 3 cm aft.
4. **Stabilator COM transforms.** `UnitPart.ModifyMass` sets `rb.centerOfMass` from the part's `centerOfMass` transform when it has one; the two elevators do. The game's stab force point is 0.35 m aft and 0.2 m inboard of the bench's collider centroid. `extract2.py` now emits `comT`, `Airframe` applies it.
5. **Per-part force clamp.** `AeroJob_Math` clamps each part's aero force to 30·mass·|v|. It bites on the 50 kg stabs above ~Mach 0.65. `Plant.ForceClamp` (default on).
6. **Transonic drag factor** (Mach 0.8–1.2) is applied by the game; `Plant.GameTransonic` is now on by default.

Checked and found **not** to be the cause: the effector tables and stab/TVC control power (dynamic |H| above), TVC sign and arm (no residual correlation with nozzle angle where TVC dominates), the lift-reshape / body-lift / downwash / camber hooks (they are applied in game: with `BodyLiftShaping` off the bench's pitch RMS goes to 107, with `StabDownwash` 0 to 78), the slat area (the game re-copies `wingArea` into the job every tick), `airflowChanneling` (none on this jet), `wingEffectiveness` (1 unless damaged), the nozzle rig (visual only). The joint model is real: the parts are separate rigidbodies on `FixedJoint`s (`AeroPart.CreateJoints`), which the bench's single rigid body cannot represent — the most plausible home for the remaining roll gain.

**Unmodelled in the logs:** Atmospheric Gusts 1.2.1 was active (Moderate ×1.9, discrete pitch gusts every ~6 s, fading above 110 m/s IAS). It perturbs each part's airflow inside the aero job, invisible in the logged α/β, zero-mean: a noise floor at low IAS, not a bias.

## Fitted factors (bench-only, `Airframe` statics; all overridable as `Name=value`)
| factor | value | what it is |
|---|---|---|
| `MisWing`, `MisBody` | 0.95 | lift multiplier on the main wing panels and the fuselage sections (plant only) |
| `MisStab` | 0.72 | stabilator lift multiplier (plant only) |
| `MisRud` | 1.18 | rudder lift multiplier (plant only) |
| `IScaleRoll` / `IScalePitch` / `IScaleYaw` | 1.12 / 1.24 / 1.13 | effective inertia per axis on top of the hull inertia (≈1.0 / 1.12 / 1.03 relative to the old box inertia) |
| `CgAftShift` | −0.155 m | bench CG 15.5 cm forward (mass moved Back → Fore, so the FCS's own mass model sees it too) |

Reading them: the game's differential-stab roll power is 0.6–0.7× the model's at every AoA while its symmetric pitch power is within 10 % (per-AoA-band regressions, `derivs.py`); a single stab factor cannot express that, so 0.72 under-states pitch control power and the 1.24 pitch inertia partly compensates. If a per-axis stab factor is wanted, `MisStab` only in roll would be the cleaner split. Tying `MisWing = MisBody` costs nothing (free fit: 1.155 / 0.834, same held-out error). The CG shift is the static trim: in trimmed flight the original bench was +10 deg/s² nose-up everywhere, ≈ 8–12 cm of lift centroid at every q and AoA plus ~50 kN·m at low lift; the stores' and fuel's real positions are unknown to that accuracy.

## Closed-loop replay of the handoff's cases (bench vs game, peak |sideslip| and errors over the window)
| case | game peak β | original bench | calibrated bench | β RMS orig → cal | α RMS orig → cal |
|---|---|---|---|---|---|
| 14:35 267.7 s stop | 10.3 | 6.0 | 5.9 | 1.90 → 2.21 | 3.96 → 0.64 |
| 10:28 126.4 s benign stop | 2.6 | 2.5 | 2.3 | 0.51 → 0.36 | 5.95 → 5.62 |
| 10:28 231.4 s benign stop | 1.9 | 2.9 | 1.7 | 0.59 → 0.45 | 0.63 → 0.83 |
| 22:36 272–282 s slow unloading rolls | 3.6 | 4.7 | 6.0 | 1.95 → 2.03 | 9.74 → 9.64 |
| 22:36 50–53 s rapid 8 g reversal | 12.4 | 5.9 | 10.7 | 2.88 → 6.17 | 18.3 → 6.1 |
| 23:23 116 s 9 g reversal | 14.2 | 8.2 | 12.5 | 2.00 → 0.78 | 4.63 → 1.25 |
| 23:27 96–98 s held 9 g roll | 6.5 | 6.1 | 6.6 | 2.35 → 1.82 | 2.57 → 2.59 |
| 23:27 316 s reversal | 8.7 | 4.9 | 7.1 | 2.57 → 3.45 | 9.76 → 11.2 |
| 23:27 382 s reversal | 9.6 | 13.8 | 13.7 | 4.84 → 4.31 | 9.56 → 7.97 |
| mean | | peak err 3.07 | **1.84** | 2.18 → 2.40 | 7.23 → 5.10 |

The high-speed reversals' sideslip peaks now land within 1.5–2° of the game (were 4–6° low). The 14:35 stop is still 4° low: in the game the roll stops much harder there (−72 → 0 deg/s in 0.5 s, bench −57 → −13) — that is the FCS's state (governors, filters) which a replay from a cold start cannot reproduce, not the plant. Windows longer than ~5 s diverge in attitude and speed (open-loop attitude), so the 12.5 s case is not meaningful beyond its first seconds.

## Telemetry proposal (next FCS build, tiny)
Once per second in `WriteRow`: the FCS's own `mass`, `cg` (root frame), `Ia` (6 unique terms), `ac.rb.inertiaTensor` and `inertiaTensorRotation`, `ac.rb.centerOfMass`, `Time.fixedDeltaTime`; every row: the throttle input (`rawThrottle`) and the fuel quantity. With those the mass and CG stop being fitted, and the inertia question (hull vs jointed assembly) is settled directly.

## Files
- `sim/Plant.cs`: `Step` split into `EnginesStep` / `AirbrakesStep` / `Forces` / `Integrate` (byte-identical results), `AngAccel`, `ForceClamp`, defaults above.
- `sim/EqErr.cs` (eqerr mode + `Poser`), `sim/Replay.cs` (replay mode), `sim/Airframe.cs` (hulls, `comT`, factors, `CgAftShift`), `sim/Program.cs` (modes).
- `extract/extract_hulls.py` (run after `extract2.py`), `extract/extract2.py` (`comT`).
- `calib/`: `eqcore.py`, `eqcmp.py`, `derivs.py`, `fit.py`, `evalset.py`, `replay_eval.py`, `f22_cases.json`, `variant.sh`.
