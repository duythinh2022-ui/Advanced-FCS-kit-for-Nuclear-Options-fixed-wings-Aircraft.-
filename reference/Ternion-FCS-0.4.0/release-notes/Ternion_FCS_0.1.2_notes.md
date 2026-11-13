# FS-3 Ternion FCS 0.1.2: elevators, canards, LERX, F-22E rollout brake, corkscrew pitch onset

Base: 0.1.1. Compiled and benched. **Not flown.** 0.1.1 is kept as `.dll.0.1.1.bak`.

## Tom (7 Nov, 00:22–00:23)
- "reduce it to 30 and make it 1.2x more effective. Make canards 1.1x more effective and buff the lerx so that the CoL is the same as before. Also copy the same airbrake on ground from the raptor. Pitch is delayed from the start of every corkscrews"
- "also the jet seems to struggle to maintain 60aoa."

No Ternion telemetry CSV exists yet (`Telemetry` is still false in the dev file). Everything below is from the bench.

## Changes
### 1. Aft elevators: stall at 30°, ×1.2 effective
- **`ElevatorStallAoA` 30.** Was 0, the game's 45° peak.
- **How the curve changes:** knee-preserving, as for the flaperons. The curve is unchanged below the 17° knee, and the peak CL of 1.0 now sits at 30° local.
- **`ElevatorEffectiveness` 1.2 (new key).** It scales `elevator_L/R` wing area by 1.2.

### 2. Canards ×1.1
- **`CanardEffectiveness` 1.1 (new key).** It scales `canard_L/R` wing area by 1.1.

### 3. LERX buffed to keep the aerodynamic centre: `LerxEffectiveness` 1.13
- **What it scales:** the shoulder LERX (`shoulder_L/R`, `SmallFighter1_LERX` foil, 7.41 m² each, 7.2 m forward). It now carries the existing `LerxEffectiveness` key; on the T/A-30 that key was the `wingroot1` strakes.
- **High-lift device:** the shoulders' HLD retracted / deployed areas scale with it, with the device's own increment kept. The FCS's flap system writes the area every tick, so this keeps the multiplier from being overwritten.
- **Sizing:** the bench's static-margin tool measures SM = −(dM/dα)/(dL/dα) at neutral surfaces. Averaged over −5…10° AoA:

| | 300 km/h | 500 km/h |
|---|---|---|
| 0.1.1 airframe | −0.286 m | −0.287 m |
| Elevators ×1.2 + canards ×1.1, LERX ×1.0 | −0.204 | −0.206 |
| LERX ×1.12 | −0.279 | −0.282 |
| **LERX ×1.13 (chosen)** | **−0.286** | **−0.289** |

- **Point by point** at −10…15° it matches 0.1.1 within 0.02 m (for example −0.57 / −0.21 / +0.10 at 0 / 5 / 10° against −0.56 / −0.21 / +0.08).
- **The aerodynamic centre is where it was in the linear range.** Above the elevators' new 30° stall the high-AoA pitch moments are different by design:
  - At 25–35° there is more nose-down: neutral-surface q-dot at 400 km/h is 4.7 / −2.0 / −1.3 °/s², against 11.0 / 12.5 / 14.9.
  - At 45–60° there is a little less: 4.6–7.7 against ~7.

### 4. Ground airbrake: the F-22E's set
- **`GroundBrakeElevator` 35 (new key):** in the rollout aero-brake the elevators go fully trailing edge up, as the F-22E's stabilators do. 0.1.0–0.1.1 kept them on the pitch stick.
- **The rest, as before:**
  - flaperons and rollervons trailing edge up 25;
  - rudders toed in;
  - high-lift devices deployed.
- **The canards stay fully leading edge down** (`GroundBrakeCanard` 60, which is drag). The F-22E has no canards. Their nose-down moment also offsets the elevators' trailing-edge-up nose-up on the nose wheel. Set `GroundBrakeCanard` 0 for the pure F-22E set.
- **Not benched.** The bench has no Ternion ground model.

### 5. Corkscrew pitch onset: `LateralStabReserve` 0.7 → 0
**Cause:**
- With pitch and lateral stick in together, the pitch law builds pitch rate on the nozzles' authority plus only (1 − 0.7 × |lateral stick|) of the stabs' remaining authority. That is the F-22E / Revoker rule: the nozzles take the pitch and the stabs stay for roll / yaw.
- The FS-3 has no nozzle pitch. At full lateral stick, pitch was built on 30 % of the canard / elevator authority: `aUpPitch` 55 against 172 °/s² in a plain pull.
- Meanwhile the trailing edges alone give ~1,200 °/s² of roll.

Bench, full aft + full roll from t = 0 vs a plain full-aft pull:

| | 0.1.1 corkscrew | 0.1.2 corkscrew | Plain pull |
|---|---|---|---|
| Pitch rate at 0.3 / 0.5 / 0.7 s, 300 km/h | 6.8 / 13.0 / 18.6 | **13.0 / 30.8 / 36.6** | 14.2 / 31.3 / 45.0 |
| Time to 15 / 30°, 300 km/h | 1.22 / 2.67 s | **0.67 / 1.43 s** | 0.62 / 1.17 |
| Pitch rate at 0.3 / 0.5 / 0.7 s, 450 km/h | 12.6 / 24.5 / 28.5 | **27.5 / 51.5 / 53.7** | 28.5 / 55.9 / 57.4 |
| Time to 15 / 30°, 450 km/h | 1.20 / 2.47 s | **0.58 / 1.07 s** | 0.55 / 1.02 |
| Peak sideslip, 300 / 450 / 600 | 1.2 / 2.1 / 2.5° | 1.6 / 2.4 / 3.0° | — |
| Held-corkscrew vel.-vector roll (2–6 s), 300 / 450 | 33.0 / 34.8 °/s | 30.3 / 31.3 | — |

**The cost:** about 0.3–0.5° more peak sideslip, and 3 °/s less roll in the held corkscrew. The AoA is up sooner, and the roll is spent on a higher AoA. Set it back in the dev file (`LateralStabReserve`) if you prefer the old split.

## "Struggles to maintain 60": what the bench says
Full aft with `AoALimitPositive` 60 at 3 km, afterburner:

| Entry | 0.1.1: peak, AoA at 10 s (cap) | 0.1.2: peak, AoA at 10 s (cap) | Speed at 10 s |
|---|---|---|---|
| 250 km/h | 49.6, 39.3 (39.6) | **56.4**, 36.9 (44.3) | 155 km/h |
| 350 km/h | 53.0, 40.3 (40.7) | **58.4**, 35.2 (45.7) | 177 km/h |
| 450 km/h | 54.1, 42.6 (42.9) | **57.4**, 40.7 (49.7) | 208 km/h |
| 550 km/h | 53.8, 47.3 (47.5) | **59.4** (59° from 8.6 s), 58.2 (58.2) | 220 km/h |

**What gets better:** the new airframe gets much closer to 60, reaching 56–59.

**What doesn't:** nothing holds 60° once the pull has bled the speed below ~200 km/h.
- From a 250–450 km/h entry the jet sits at 55–58° at ~5 s and ~200 km/h. The canards are on their 60° leading-edge-down stop and the elevators on their 35° stop.
- The authority cap (where the canards and elevators can still bring the nose down) then retreats to 44–50 within ~1.5 s.
- The jet follows it down. With nose-up authority of only ~14 °/s² at 155–180 km/h, it undershoots to 35–40°, 5–10° under the cap.

**This undershoot is new in 0.1.2.** It comes with the bigger surfaces and LERX: with `ElevatorStallAoA` 0 it is the same, and with `LateralStabReserve` 0.7 it is the same. The jet now gets higher before the speed runs out, so it has further to come down.

**Tried and left off:** sweeping the authority cap at the IAS the deceleration will bring (`FcsController.AuthCapPredict`, a bench switch). The AoA at 10 s moved only from 35–37 to 38°.

**A real 60° hold at low speed** needs more nose-down and nose-up authority than this airframe has without TVC. Alternatively, cap the held AoA by speed so the jet doesn't climb to 58° just to fall back.

## Unchanged checks (default limit 40)
| Case | Result |
|---|---|
| Loaded rolls at 40°, 250–450 km/h | AoA 38.6–41.1, sideslip ≤ 2.0 (0.1.1: 39.4–41.2, ≤ 1.6) |
| 1 g rolls, 300 / 500 / 700 km/h | 169 / 228 / 251 °/s, sideslip ≤ 1.5 |
| Pull 0.6 and release | Settles to 0 |

## Not verified
- **Not flown.** The bench is uncalibrated for this jet.
- **Rollout brake set:** not benched.
- **For the next flight:** set `Telemetry = true` in `Ternion_FS3.FCS.Dev.cfg`.

## Install (done on your PC)
- **Main:** `plugins\Ternion_FS3.FCS\Ternion_FS3_FCS.dll` (md5 `ab0d8daf3b3df8051a6c4da2aac0e7d2`), with `meta.json` 0.1.2. 0.1.1 is kept as `.dll.0.1.1.bak`.
- **Dev unlock:** `plugins\Ternion_FS3_FCS_Dev.dll` 0.1.2.
- **Config:** your dev file still holds 0.1.1's values for the keys whose defaults changed:
  - `ElevatorStallAoA` 0 → **30**;
  - `LerxEffectiveness` 1 → **1.13**;
  - `LateralStabReserve` 0.7 → **0**.

  0.1.2 moves these three once, only if a key still holds the old default. The new keys (`ElevatorEffectiveness`, `CanardEffectiveness`, `GroundBrakeElevator`) take their defaults.
- **`LogOutput.log`:** "FS-3 Ternion FCS 0.1.2 … ready" and "Ternion FCS 0.1.2 defaults moved once: …".
- **Respawn** for the airframe changes.
- **Release folder:** `C:\Games\NO mods\Ternion-FCS-0.1.2-release`.
