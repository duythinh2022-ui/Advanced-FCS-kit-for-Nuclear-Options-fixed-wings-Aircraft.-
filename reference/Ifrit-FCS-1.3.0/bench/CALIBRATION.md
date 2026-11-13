# Ifrit bench calibration against the 28–30 Oct flight logs (31 Oct 2026)

Bench-only. No FCS behaviour change, no tuning value touched. Everything below is from the bench and the logs — nothing here was flown. Same method as the F-22E calibration (`F22E_bench_calibration_2.4.18.md`): equation-error mode, root causes in code first, few named factors after.

## Data — thin
Only one log on the current game build: `Ifrit_FCS_20261030_230226.csv` (0.5.6), 1,956 mode-3 airborne samples at 60 Hz = **33 s of flight**. The 28 Oct log (`20261028_225959`, 0.5.5, previous game build) has 1,197 samples (20 s) and is used as the held-out check. The four 27 Oct logs are from an older FCS build with a different column set and do not score (roll RMS 140 — column mismatch, not physics); they were dropped. With 33 s there is no room for a multi-parameter fit (see "What was not fitted" below) — the numbers here are the code fixes plus one static-trim factor. Another two or three logs of varied manoeuvring on the current build would make a real fit possible.

## Method
`bench eqerr log=<csv> out=<csv>`: the plant posed at every logged sample (TAS, AoA, sideslip, p/q/r, rho, attitude), elevators/ailerons/flaps/rudder at their logged `now_*`, split flaps at `splitL/R`, nozzles at `tvcNow ± tvcdNow` (per side, by the nozzle's lateral position) and `tvcyNow`, slats at `hlPos`, airbrake at `abOpen`, each nozzle's thrust = logged `tw` × m × g / nozzles (`Plant.ThrustOverride`). `Plant.Forces` (the game's AeroJob_Math + the FCS hooks) → predicted angular accelerations (I⁻¹(M − ω×Iω)) and nz, compared with Savitzky–Golay derivatives (9 samples, cubic) of the logged rates. Tools in `calib/` (`eqcore.py`, `eqcmp.py`, `derivs_stock.py`, `fit.py`, `evalset.py`, `multi_eval.py`).

## Equation-error RMS (predicted − measured; deg/s², nz in g)
30 Oct log (current build), spread of the measured accelerations: roll 47.2, pitch 25.4, yaw 13.9, nz 0.90.

| 30 Oct log | roll | pitch | yaw | nz |
|---|---|---|---|---|
| **original bench** (50 % fuel = 20,140 kg, 0.02 s tick, camber coupling missing) | 16.45 (scale 0.79) | 11.45 (bias +5.8) | 3.90 (scale 0.81) | 0.67 (bias +0.25, scale 0.62) |
| mass 25.5 t + 60 Hz tick + force clamp + transonic + profile Thrust/DragScale | 22.3 | 23.7 | 5.4 | 0.48 |
| + camber-coupling host fix | 9.2 | 9.6 | 2.4 | 0.36 |
| **calibrated** (+ `CgAftShift` −0.05) | **9.57** (scale 0.99) | **9.97** (bias +4.7) | **2.42** (scale 0.93) | **0.36** (bias +0.08) |

| 28 Oct log (held-out, previous game build, 0.5.5) | roll | pitch | yaw | nz |
|---|---|---|---|---|
| original bench | 8.73 (scale 0.49) | 26.4 (bias +16.0) | 2.25 | 1.27 (bias +0.74) |
| calibrated | 7.81 (scale 0.63) | 25.05 (bias +13.9) | 1.74 | 0.65 (bias +0.26) |

The 28 Oct log is a gentle one (roll spread 8.6, pitch 43 — mostly a long high-AoA pull): its pitch error is almost all the static bias, see below.

## Root causes fixed in code
1. **Physics tick** 60 Hz (`Plant.dt = 1/60`; was 0.02 s).
2. **Mass.** The logged T/W ceiling at full afterburner is 1.17 at lift-off; through the game's thrust model (static·dyn − loss)·spool + AB·clamp(ρ,0.4,1) with the Buffed `ThrustScale` 1.08 that is **~25.5 t**: full tanks at spawn (`DefaultFuelLevel` 1) plus ~1.3 t of stores. The bench flew 50 % fuel, 20,140 kg. Default now `FuelFrac = 1`, `MassTarget = 25500` (the ballast goes on the fuselage parts).
3. **Thrust/drag profile.** `IfritTest.Defaults` forced `ThrustScale`/`DragScale` to 1; the game runs the config's Buffed profile (1.08 / 0.95). Removed — the bench now uses the config, as in game.
4. **Camber coupling not applied.** The bench renames the stock surfaces (`elevator_L` → `Elevator_L`, …) so the F-22E-style FCS code matches, but `Camber.HostOf` / `Camber.KForName` are keyed on the game names, so the flap/aileron camber coupling (`CamberForce` in game) silently never fired in the bench. `Airframe.GameName()` reverses the rename at the three lookups (`Plant.BuildCamber`, the camber force, and `FcsController` via `isync.sh`). Roll RMS 22.3 → 8.9 on its own — this was the single largest bench error, and it means every earlier Ifrit bench roll result under-stated the ailerons' effect.
5. **Per-part force clamp** (30·mass·|v|, `Plant.ForceClamp`) and the **transonic drag factor** (`Plant.GameTransonic`) on by default, as in game.
6. `Plant.ThrustOverride` (eqerr only): per-nozzle thrust from the logged T/W instead of the engine model, so the mass back-out and the thrust moment are consistent.

## Fitted factor
| factor | value | what it is |
|---|---|---|
| `CgAftShift` | −0.05 m | bench-only static trim: 5 cm of mass moved from the aft to the forward centreline part (parts ≥ 300 kg, |y| < 0.5 m, so the move stays symmetric). The foremost part is light, so larger values saturate at 0.6× its mass. |

**What was not fitted, and why.** A free fit of `MisLift`, `CgAftShift`, `IScaleRoll` on the 30 Oct log (33 s) went to `MisLift` 0.67 / `CgAftShift` −0.21 and made the held-out 28 Oct roll and yaw worse (7.75 → 8.46, 1.92 → 2.53) while only moving its pitch bias from +13 to +9: it was fitting the 30 Oct manoeuvre, not the jet. A 3-parameter `MisBody` fit went to 0.16 — nonsense. So `MisWing`, `MisBody`, `MisStab`, `MisRud`, `MisFin`, `IScale*` stay at 1 and are only exposed as overridable statics (`Name=value`) for the next calibration with more data. The inertia is the AABB-box inertia of the stock colliders: the Ifrit's collider meshes stream from `resources.assets.resS` (1 GB), which could not be staged, so no hull mass properties this time (on the F-22E the hulls were 9–11 % lighter than the boxes).

## What is still wrong (bench vs game), from the per-AoA-band regressions (`derivs_stock.py`)
- **Static nose-up bias**, growing with AoA: pitch bias (pred − meas) +3 deg/s² at 0–5°, +6–9 at 5–36°, +14 at 36–45°, +19 at 45–55° on the 30 Oct log; +14 overall on the 28 Oct log, which lives at 20–40°. On the F-22E the same symptom was a CG offset; here `CgAftShift` saturates at −0.05 because the nose part is light, and the AoA dependence says it is a lift-centroid / downwash term as much as a CG one. The per-band regression puts the game's AoA (α·q̄) pitch term at 1.5–2.1× the bench's between 10° and 90° — the game's nose-down restoring moment with AoA is stronger than the bench's. Candidates: how the bench applies the stock `AircraftParameters` pitch-stability terms, the tail's downwash at high AoA, the stores' real positions. Unresolved.
- **Post-stall lift**: at 36–55° AoA the bench's nz is 0.3–0.7 g above the game's (nz scale 0.78 overall, mostly from those bands); within ±0.2 g below 36°. Unresolved; it only matters above 36°.
- **TVC**: the game's pitch TVC term (T·sin tvc) is 1.09–1.24× the bench's at 10–90° AoA and its differential-TVC roll term 1.1–1.6×. The Revoker calibration found the same (its fitted `NozzleArmScale` = 1.12); the Ifrit bench has no such factor yet because 33 s of data cannot fit it — the Revoker's value is the obvious thing to try when more logs exist.
- Control surfaces (game / bench, per AoA band, 30 Oct log): ailerons 0.91–0.98 in roll (0–45°), symmetric elevators 0.95–1.19 in pitch, differential flaps 1.0–1.5, split flaps ~1.0–1.2. Fine within this data's noise.
- Gusts: Atmospheric Gusts 1.2.1 was active in these logs (Moderate ×1.9, discrete pitch gusts, fade above 110 m/s IAS) — zero-mean noise, not a bias; part of the floor on both logs.

## Telemetry proposal (next FCS build, tiny)
Once per second in `WriteRow`: `ac.rb.mass`, `ac.rb.inertiaTensor`, `inertiaTensorRotation`, `ac.rb.centerOfMass` (root frame), `Time.fixedDeltaTime`, and the FCS's own mass / cg / inertia if it keeps them; every row: the throttle input and the fuel quantity. Mass and CG then stop being inferred from T/W; the box-vs-hull inertia question is settled directly, and the nose-up bias can be split between CG and aero. For the Ifrit specifically, logging the split-flap and TVC-yaw commanded-vs-actual (`splitL/R`, `tvcy` already there) is enough.

## Files
- `sim/Plant.cs`: `dt` 1/60, `GameTransonic`, `ForceClamp`, `ThrustOverride`, `Forces`/`AngAccel` split, `Airframe.Mis()` in the lift term, `GameName()` at the camber lookups.
- `sim/Airframe.cs`: `GameName()`, `Mis*`/`MisLift`/`IScale*`/`CgAftShift` statics, `CgAftShift` applied in `MassProps` (centreline parts only), ballast for `MassTarget` on the fuselage.
- `sim/EqErr.cs` (eqerr mode: pose, split flaps, per-side TVC, TVC yaw, thrust override), `sim/IfritTest.cs` (`Defaults`: mass 25.5 t / full fuel / `CgAftShift`, no Thrust/DragScale override), `sim/Program.cs` (`eqerr`).
- `fcs/` is regenerated by `isync.sh` (which now also patches the camber host lookup in `FcsController.cs` and the `SplitRange` stub line).
- `calib/`: `eqcore.py`, `eqcmp.py`, `derivs_stock.py`, `fit.py`, `evalset.py`, `multi_eval.py`.
- Airframe data: `extract/ifrit_airframe.json` (name-based `stock_extract.py` — the 30 Oct game update changed the asset path IDs the old extractor used).
