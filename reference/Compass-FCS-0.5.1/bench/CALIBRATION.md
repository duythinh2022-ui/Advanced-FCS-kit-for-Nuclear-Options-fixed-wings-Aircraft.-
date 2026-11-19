# Revoker bench calibration against the 30 Oct flight logs (31 Oct 2026)

Bench-only. No FCS behaviour change, no tuning value touched. Everything below is from the bench and the logs — nothing here was flown. Same method as the F-22E calibration (`F22E_bench_calibration_2.4.18.md`): equation-error mode, root causes in code first, few named factors after, fitted on some logs and checked on a held-out one.

## Data
Logs `Revoker_FCS_20261030_{145120,224811,225412,235509}.csv` (0.2.13–0.2.14, current game build, Buffed profile), mode 3, airborne, TAS > 40 m/s: 30,896 samples at 60 Hz (8.6 min). Training: 225412, 235509, 145120 (25k). Held-out: 224811 (5.9k). Two more logs were not used for the fit: `101038` (17 min; the jet burns from ~12 t to ~9.4 t over it, and the bench has one mass per run — it needs the fuel-quantity telemetry below) and `234500` (49 s, ~13.8 t loadout); both are scored below as extra checks.

The Revoker's mass per log is backed out of the logged T/W ceiling at full afterburner through the game's thrust model (`Turbojet` static·dyn − loss, AB·clamp(ρ,0.4,1), Buffed `ThrustScale` 1.08): 11.8 t (225412), 12.1 t (235509), 12.2 t (145120), 12.6 t (224811). Empty + full tanks is 11,880 kg, so these are full fuel plus 0–0.7 t of stores; the bench puts the ballast on the two weapon-bay parts (putting it on the tail doubled the pitch error).

## Method
`bench eqerr log=<csv> out=<csv> [MassTarget=<kg>]`: the plant posed at every logged sample (TAS, AoA, sideslip, p/q/r, rho, attitude), canards/ailerons/rudder at their logged `now_*`, hinge flaps from `flapHL/HR`, split flaps from `splitL/R`, nozzles at `tvcNow ± tvcdNow` per side and `tvcyNow`, slats at `hlPos`, each nozzle's thrust = logged `tw` × m × g / nozzles. `Plant.Forces` (the game's AeroJob_Math + the FCS hooks) → predicted angular accelerations (I⁻¹(M − ω×Iω)) and nz, compared with Savitzky–Golay derivatives (9 samples, cubic) of the logged rates. `calib/fit.py` (Nelder–Mead, `--perlog` for the per-log mass), `multi_eval.py`, `derivs_stock.py` (per-AoA-band control / stability derivatives, game vs bench), `eqcmp.py` (error maps).

## Equation-error RMS (predicted − measured; deg/s², nz in g)
Spread of the measured accelerations (4 logs): roll 52.2, pitch 36.1, yaw 23.7, nz 1.36.

| 4 logs | roll | pitch | yaw | nz |
|---|---|---|---|---|
| **original bench** (50 % fuel = 10,280 kg, Thrust/DragScale 1, 0.02 s tick) | 13.92 (scale 0.83) | 17.10 (bias +10.8) | 5.41 (scale 1.14) | 0.49 (bias +0.3, scale 0.82) |
| code fixes + per-log mass, stores on the bays | 10.29 (scale 0.92) | 12.84 (bias +7.1) | 3.74 (scale 1.10) | 0.30 (scale 0.97) |
| **calibrated** (+ 4 factors) | **9.57** (scale 0.96) | **6.91** (bias −0.5) | **2.96** (scale 0.97) | **0.30** (scale 0.97) |

| per log | original | calibrated |
|---|---|---|
| 145120 (train) | 6.88 / 17.73 / 3.39 / 0.57 | 6.19 / 5.88 / 2.13 / 0.30 |
| 225412 (train) | 13.29 / 14.78 / 5.69 / 0.39 | 8.71 / 5.69 / 2.94 / 0.25 |
| 235509 (train) | 14.27 / 16.90 / 5.91 / 0.53 | 11.47 / 9.74 / 3.48 / 0.38 |
| **224811 (held-out)** | 18.82 / 21.72 / 5.47 / 0.60 | **11.53 / 6.76 / 2.97 / 0.30** |
| 234500 (extra, 13.8 t) | 7.94 / 21.34 / 5.46 / 0.51 | 4.49 / 7.65 / 2.60 / 0.27 |
| 101038 (extra, 17 min, mass drifts 12 → 9.4 t, run at 9.4 t) | 11.42 / 15.71 / 5.01 / 0.47 | 13.11 / 9.65 / 4.34 / 0.39 |

With one default mass (12,200 kg) instead of the per-log values the 4-log numbers are 9.62 / 7.38 / 3.04 / 0.30 — the per-log mass is worth 0.5 deg/s² in pitch, not more. On 101038 the calibrated roll is worse (scale 1.11: the game rolls harder than the bench there) — that log's mass and inertia change by 20 % over 17 minutes and a single-mass run is wrong for most of it either way; its pitch, yaw and nz still improve.

## Root causes fixed in code
1. **Physics tick** 60 Hz (`Plant.dt = 1/60`; was 0.02 s).
2. **Mass and stores.** 50 % fuel and no stores → full tanks (`FuelFrac = 1`, the game's `DefaultFuelLevel`) plus `MassTarget` (default 12,200 kg, override per log) with the ballast on `weaponbay*`. Pitch bias +10.8 → +7.1 from this alone.
3. **Thrust/drag profile.** `IfritTest.Defaults` forced `ThrustScale`/`DragScale` to 1; the game runs the config's Buffed profile (1.08 / 0.9). Removed — the bench uses the config, as in game.
4. **Dedicated airbrake** support (`Airframe.airbrakes` from the extracted `airbrakes`, drag = dragAmount·ρ·v²·open at the part's rigidbody, as the game's `Airbrake.FixedUpdate` does). The extractor finds no `Airbrake` component on the Revoker (or Ifrit) prefab — the FCS's `abOpen` on these jets is the split-flap brake, which the pose already carries — so the list is empty here; the code is there for jets that have one.
5. **Per-part force clamp** (30·mass·|v|, `Plant.ForceClamp`) and the **transonic drag factor** (`Plant.GameTransonic`) on by default, as in game.
6. **Extractor**: the 30 Oct game update changed the asset path IDs the old extractor hard-coded; `stock_extract.py` now finds the prefab (`Fighter1`) and its `AircraftParameters` by name and emits parts (with `comT`), surfaces (split drag, yaw split factor, hinge flaps), engines, nozzles, slats, tanks, gears, airbrakes, airfoils, the stock FBW and the AoA limiter.

## Fitted factors (bench-only; `Plant` / `Airframe` statics, overridable as `Name=value`)
Fit: 6 candidates (`MisFin`, `NozzleArmScale`, `CgAftShift`, `IScaleRoll`, `IScalePitch`, `IScaleYaw`) on the three training logs, weights 1/spread per axis. `MisFin` and `IScaleYaw` were not identifiable (`MisFin` wandered 1.26 → 0.88 with the cost flat; `IScaleYaw` stayed at 1.00) and are left at 1; the other four, rounded, reproduce the full fit to 0.1 deg/s².

| factor | value | what it is |
|---|---|---|
| `NozzleArmScale` | 1.12 | thrust moment lever arm about the CG (thrust force unchanged). Before the fit the game's TVC pitch and yaw terms were 1.13–1.3× the bench's at every AoA band; after it they are 0.96–1.09 (pitch) and 0.97–0.99 (yaw). Either the nozzle transforms sit further aft in game than the extracted hierarchy says, or the game's CG is further forward than the box-collider mass properties give — the same ambiguity the F-22E hulls resolved there. |
| `CgAftShift` | −0.10 m | bench-only static trim: 113 kg moved cockpit → tail so the CG is 10 cm forward. Pitch bias +7.1 → −0.5. |
| `IScaleRoll` | 1.04 | effective roll inertia on top of the box-collider inertia |
| `IScalePitch` | 0.96 | effective pitch inertia |

The inertia is the AABB-box inertia of the stock colliders (the Revoker's collider meshes stream from the 1 GB `resources.assets.resS`, which could not be staged); on the F-22E the hulls were 9–11 % lighter than the boxes on every axis, so a 1.04 / 0.96 here is well inside what the hull-vs-box difference could be, and should be re-fitted once the hulls (or the proposed `inertiaTensor` telemetry) are available.

## What the calibrated bench gets right and wrong (`derivs_stock.py`, game / bench per AoA band, 4 logs)
- Canards 0.93–1.17 in pitch, ailerons 0.91–0.99 in roll, rudder 0.89–1.09 in yaw, TVC pitch 0.96–1.09, TVC yaw 0.97–0.99; pitch damping 0.5–1.5 and yaw damping 0.6–1.3 (both noisy — damping is a small term next to the controls here) — all fine.
- **Fin weathervane** (yaw moment with sideslip): the game's is 1.10–1.31× the bench's from 10° AoA up (0.93–0.98 below). `MisFin` could not carry it because the fin's lift also enters roll and pitch; a yaw-only sideslip factor would be the clean fix if it matters for the FCS's beta loops. Note the bench's yaw RMS of 2.96 against a spread of 23.7 is already an R² of 0.98.
- Roll: R² 0.97 overall but the residual is concentrated at 5–15° AoA in the fastest rolls (RMS 13–14 where the spread is 80–100): a flat ~4 % gain (`scale 0.96`), probably the jointed-assembly effect noted on the F-22E (the parts are separate rigidbodies on `FixedJoint`s; the bench is one rigid body).
- Pitch bias is within ±2 deg/s² at 0–90° AoA; +4.6 / +5.3 at −45…−10° (inverted / negative-AoA flight — the stock airfoil's negative side), −3.3 at −10…0°.
- With the split flaps / brake out (3 % of samples) the pitch bias is −3.6 deg/s² (nose-down) against −0.4 elsewhere; small, not chased.
- Gusts: Atmospheric Gusts 1.2.1 was active in these logs (Moderate ×1.9, discrete pitch gusts, fade above 110 m/s IAS) — zero-mean noise, part of the floor.

## Telemetry proposal (next FCS build, tiny)
Once per second in `WriteRow`: `ac.rb.mass`, `ac.rb.inertiaTensor`, `inertiaTensorRotation`, `ac.rb.centerOfMass` (root frame), `Time.fixedDeltaTime`, and the FCS's own mass / cg / inertia; every row: the throttle input and the fuel quantity (`FuelBalance` has it). With those the per-log mass back-out and `CgAftShift` go away, long logs like 101038 become usable (mass per sample), and `NozzleArmScale` can be split between a CG error and a nozzle-position error.

## Files
- `sim/Plant.cs`: `dt` 1/60, `GameTransonic`, `ForceClamp`, `ThrustOverride`, `NozzleArmScale`, airbrake drag, `Forces`/`AngAccel` split, `Airframe.Mis()` in the lift term.
- `sim/Airframe.cs`: `airbrakes`, `MisFin`/`MisCan`/… and `IScale*`/`CgAftShift` statics, `CgAftShift` in `MassProps` (centreline parts only), `MassTarget` ballast on the weapon bays.
- `sim/EqErr.cs` (eqerr mode: pose, hinge and split flaps, per-side TVC, TVC yaw, thrust override), `sim/IfritTest.cs` (`Defaults`: the calibrated values above), `sim/Program.cs` (`eqerr`).
- `fcs/` is regenerated by `rsync.sh` (which now also patches the `SplitRange` stub line).
- `calib/`: `eqcore.py`, `eqcmp.py`, `derivs_stock.py`, `fit.py` (`--perlog`), `evalset.py`, `multi_eval.py`, `stock_extract.py`.
- Airframe data: `extract/rev_airframe.json`.
