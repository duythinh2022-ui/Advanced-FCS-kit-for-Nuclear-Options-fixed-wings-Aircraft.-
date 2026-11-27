# T/A-30 Compass FCS 0.1.0

New mod, separate DLL (`Compass_TA30.FCS`). It is a fork of FS-12 Revoker FCS 0.8.2: the F-22E / Ifrit / Revoker control laws (reference governor, INDI allocator, AoA / G protection, asymmetric-load and engine-out handling, fuel balancing, multiplayer host checks), retargeted to the stock T/A-30 Compass (prefab `trainer`). The Revoker-only parts are off or gone: canard travel and window tuning, yaw TVC, elevon stall stagger, and the Revoker aileron hinge visuals.

**Status:** compiled against the installed game and benched on the Compass airframe extracted from `resources.assets`. **Not flown.** No Compass flight logs exist, so the bench is uncalibrated for this jet: use it for direction and A/B, not absolutes.

## Tom (5 Nov, 16:01–16:03)
- "make fcs mod for compass, add buffed variant with 8% more thrust, 25% less fuel burn per throttle, same ew and counter measure buff as the other 3. Add camber coupling to rudders, flaps and ailerons, add downwash modelling for stabs. Make it relaxed stability like moving the com back or make lerx more effective, find the right config to that"
- "increase actuator speed like on the other 3, find the max usable aoa for it"

## The airframe (stock, from the game files)
- **Surfaces:**
  - Elevators `elevator_L/R`: all-moving slabs (1.8 m² each, own `Trainer_stabilator` foil). Pitch 30 + roll 10 = ±40.
  - Fixed `hstab_L/R`: 0.63 m² each.
  - Ailerons `aileron_L/R`: ±30.
  - Twin rudders `rudder_L/R`: ±25, on canted fins `vstab2_L/R`.
  - Stock servos: 70 °/s (rudders 40).
- **Flaps** `flap_L/R`: moved only by the high-lift device. 20° TE-down below 80–120 m/s; the same device turns `wing1_X`'s lift normal 10°.
- **Engines:** 2 × Turbojet, 31.6 kN, no afterburner, no TVC.
- **Airbrake:** one dedicated airbrake.
- **Wing foil** `Trainer_wing`: CL 1.0 at 17°, 0.8 at 23°, back to 1.0 at 45°.
- **Mass:** 7,043 kg at the game's default 45 % fuel.
- **Stock FBW:** 8 g, α limiter ~15°.
- **Stock static margin:** +0.45 m (very stable).
- **LERX equivalent:** `wingroot1_L/R`, 0.45 m² each, 2.2 m ahead of the CG.

## What 0.1.0 changes on the airframe
| Item | Setting | What it does |
|---|---|---|
| Actuators | `SurfaceServoSpeed` 100 | Every surface at 100 °/s (stock 70, rudders 40), as on the other three |
| Tail downwash | `StabDownwash` 0.45, `StabDownwashLiftDriven` true | Elevators and hstabs fly in the wing's downwash. ε = k·CL_wing(α)/CLα: equals k·α up to the 17° stall, then tracks the wing's post-stall lift; faded out 60–90°. Game side: `DownwashForce` (not attached on the Revoker); FCS side: the elevator models |
| Aileron camber | `AileronCamberCoupling` 0.55 | The wingtip ahead turns by K × aileron deflection. Thin-airfoil: 0.47-chord aileron, τ 0.76 → (0.76×6.37 − 3.0)/3.37 |
| Rudder camber | `FinCamberCoupling` 0.55 | `vstab2` turns by K × rudder deflection. 0.52-chord rudder, τ 0.78 |
| Flap camber | `FlapCamberCoupling` 0.40 | Flaps are allocator effectors (`FlapFlaperons`), ±20; `wing1` turns by K × flap. 0.25-chord flap, τ 0.55. The stock HLD uses 0.5 |
| Landing flaps | `FlapHighLiftDroop` 20 | Flaps follow the stock HLD schedule (20° TE-down below 80–120 m/s); the allocator adds roll / pitch on top |
| Relaxed stability | `CgShiftAft` 0.27 m | 202 kg moved `cockpit_F` → `tail` at 45 % fuel (scaled to spawn mass); both rigidbodies updated. Fuel balancing's reference reads the part masses, so it holds the new CG |
| LERX | `LerxEffectiveness` 1 | Available (wingroot1 lift ×), not used by default; see below |
| Elevator travel | `StabilatorRangeTEUp/Down` 0 | Stock ±40 (the Revoker's 60/45 canard buff is not carried over) |
| Rollout brake | `GroundBrakeCanard` 0 | Elevators stay on the stick. On an aft tail, trailing edge up lifts the nose wheel. Ailerons and flaps go up and the rudders toe in |

### Relaxed stability: CG aft vs LERX (bench, 400 km/h, downwash 0.45 on)
| Config | Static margin at 0–5° | Trim elevator at 10 / 30° | q̇ at full nose-up elevator, 35° |
|---|---|---|---|
| Stock (no downwash) | +0.45 m | 12.6 / >40 (can't trim) | −56 °/s² |
| Downwash only | +0.27…+0.29 m | 7.6 / 34.9 | −19 |
| **CG aft 0.27 m (chosen)** | **0.00…+0.02 m** | **0.5 / 25.1** | **+63** |
| CG aft 0.30 m | −0.03…−0.01 m | −0.4 / 24.1 | +72 |
| LERX ×4 | +0.13…+0.16 m | 4.3 / 29.9 | +21 |
| LERX ×6 | +0.05…+0.07 m | 2.1 / 27.1 | +47 |

- **CG aft is the right lever.** 0.27 m (≈13 % of a ~2 m chord) brings the margin to neutral below the stall. It does not depend on fuel: identical at 10 / 45 / 100 % fuel, because the tank centroid sits on the dry CG.
- **The LERX would need ×6:** 2.7 m² strakes, larger than the inner wing section, for a smaller effect at high AoA.
- **Above the 17° wing stall the jet is still nose-down (stable).** That is the conventional tail's stall break. The lift-driven downwash makes it stronger than the linear model would (linear: trims to ~50°). Switch `StabDownwashLiftDriven` off for the linear model.
- **AI Compasses on the stock FBW get the same airframe** (as on the other mods). Bench: the stock FBW flies it fine in pulls, rolls and pushes (peak AoA 17.5 vs 18.2 stock).

### Camber coupling (bench, 300 km/h, stability-axis deg/s²)
| AoA | Aileron roll, off → on | Rudder yaw, off → on |
|---|---|---|
| 0° | 246 → 589 | 31 → 58 |
| 10° | 259 → 493 | 39 → 69 |
| 20° | 234 → 339 | 44 → 74 |
| 30° | 117 → 127 | 42 → 65 |

The game flies the 30° aileron as its own stalled plate (17° foil), while the host wingtip at 0.55 × 30° stays attached. That is why the gain is large at low AoA. Closed loop: +3 to +5 °/s of velocity-vector roll at the 30° limit, +7 °/s in push-rolls.

## Max usable AoA: ±30°
Full-aft pulls, CG 0.27, downwash 0.45 (45 % fuel unless noted):

| Limit | Hold, 200–450 km/h | Elevator used (of 40) | Loaded roll at the limit (vel.-vector) |
|---|---|---|---|
| 28 | 28.0 ± 0.04 | 30.7–32.4 | 38–51 °/s |
| **30** | **30.0 ± 0.05** | **33.5–35.4** | **37–50 °/s, sideslip ≤ 4.1°** |
| 32 | 32.0 ± 0.05 | 36.6–38.6 | 37–48 °/s |
| 35 | sags to 29–34; elevators at the stop | 40 | 32–43 °/s |
| 40 | can't hold (33–38, oscillating) | 40 | — |

- **30 keeps ~5° (≈15 %) of elevator** for capture, gusts and the pitch share of a loaded roll. During a full roll at the limit the elevators reach 40 and the AoA dips 1–2° below the limit.
- **Negative side:** −30 is captured and held at −30.0, with 5.6° of elevator left. Push + full roll at 250 km/h: ~50 °/s, sideslip 1°. The canted rudders lose almost all stability-axis yaw past −25…−35°.
- **Aerodynamic trim limit:** ~41° with full elevator.
- **By design, not a fault:** idle from 250 km/h with full aft held, the AoA creeps to 33–34° as the jet mushes near 48 m/s. That is the F-22E 2.44 rule: full aft cancels AoA protection as a zero-rate nose hold. Protection pushes once the stick comes off.

## Other bench results (final defaults)
| Case | Result |
|---|---|
| 1 g full roll 300 / 500 / 700 km/h | 143 / 208 / 248 °/s peak; 90° bank in 0.97 / 0.67 / 0.60 s; sideslip ≤ 1.7° |
| Roll release 700 km/h | 247 → 15 °/s in 0.5 s, then exponential tail; ~3° bank overshoot |
| Full pedal 250 / 450 km/h | Sideslip 13° (`PedalSideslip` 15) |
| Pull 0.6 then release, 300 km/h | Pitch rate after release 0.2 °/s |
| Left engine out at 250 km/h, 0.5 pull | Held at the 22° engine-out cap, sideslip 0.3° |
| Buffed thrust, 20 s level at full power from 400 km/h | 718 km/h stock → 749 buffed |

## Performance profiles
| | Stock (default) | Buffed |
|---|---|---|
| Thrust | ×1.00 | **×1.08** |
| Fuel burn per throttle setting | ×1.00 | **×0.75** (25 % less) |
| Parasitic drag | ×1.00 | ×1.00 (not asked for) |
| Flares / gun ammo / EW capacity / EW recharge | ×1 | ×10 / ×2 / ×1.5 / ×1.2 (same as the F-22E, Ifrit and Revoker) |

## Envelope
| Setting | Value |
|---|---|
| AoA limits | +30 / −30 |
| G limits | +9 (the jet's rated `aircraftGLimit`; stock FBW 8) / −3.5 |
| MPO | +12.5 / −3.5 g (family default) |
| Pitch rate | 48 °/s |
| Roll / yaw rate limits | Unchanged from the Revoker |

## Coexisting with the Ifrit and Revoker FCS
- **Turbojet:** the Compass is also a `Turbojet`. This DLL installs only a prefix on `Turbojet.FixedUpdate`, for its setup, and **no transpiler**: the jet has no vectoring. The Ifrit / Revoker nozzle transpiler chain is untouched.
- **Other shared hooks:** each acts only on its own jet, as the Ifrit and Revoker already do.

## Not done / not verified
- **Not flown.** The bench is not calibrated for this jet.
- **Structure:** the bench's structural readout is Ifrit-specific and gives raw newtons here, so over-G breakup is unknown. The joints are 12–25 kN. MPO at 12.5 g may break parts.
- **Not benched:** ground handling, take-off, landing, gear-down law, and AI flight.
- **Not checked in game:** whether `hstab_L/R` (parented under the nozzles) and the elevators get the downwash. The bench applies it.
- **Not released:** no GitHub or NOMNOM release yet.

## Install (done on your PC)
- **Main:** `BepInEx\plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` (md5 `d4a7acfab9f47700118930df711e3d46`) + `meta.json`.
- **Dev unlock:** `BepInEx\plugins\Compass_TA30_FCS_Dev.dll`.
- **Config:** `BepInEx\config\Compass_TA30.FCS.cfg` (player) and `.Dev.cfg`, both written on first launch. `PerformanceProfile` defaults to Stock.
- **`LogOutput.log` should show:**
  - "T/A-30 Compass FCS 0.1.0 … ready".
  - On spawn: "Compass FCS CG shift: … kg moved cockpit_F -> tail".
  - "Compass FCS tail downwash on …: 4 tail parts".
  - "Compass FCS attached: 6 surfaces …, inboard elevons 2 (±20)".
- **Telemetry** (dev `Telemetry`): `BepInEx\Compass_FCS_<time>.csv`.

## Release folder
`C:\Games\NO mods\Compass-FCS-0.1.0-release`:
- `TA30-Compass-FCS-0.1.0.zip` (main, sorts first).
- `Optional.TA30-Compass-FCS-0.1.0-DevUnlock.zip`.
- `Source.TA30-Compass-FCS-0.1.0.zip` (with the bench, the extracted airframe and the `csweep.py` / `csuite2.py` scripts).
- These notes and `meta.json`.
