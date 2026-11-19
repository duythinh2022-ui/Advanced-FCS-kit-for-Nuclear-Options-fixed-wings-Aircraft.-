# T/A-30 Compass FCS 0.1.2: reshaped strake (LERX) lift curve, no stick bypass of AoA protection

Base: 0.1.1. Patch bump. Compiled against the game; benched; **not flown**. 0.1.1 is kept as `.dll.0.1.1.bak`.

## Tom (5 Nov, 17:02–17:03)
- "then redo the lift curve for the lerx"
- "and no zero-ratehold rule for bypass protection on non tvc jet"

## 1. The strake (LERX) lift curve
The model is the same slender-strake vortex-lift curve as 0.1.1 (Polhamus: C<sub>N</sub> = 1.57·sinα·cosα + π·sin²α, faded to a flat plate after vortex breakdown). What changes is where the vortex breaks down.

| α | 0.1.1 (breakdown 38→60°) | **0.1.2 (30→50°)** | wing foil |
|---|---|---|---|
| 10° | 0.36 | 0.36 | 0.58 |
| 20° | 0.82 | 0.82 | 0.90 |
| 30° | 1.27 | 1.27 | 0.85 |
| 32° | 1.35 | **1.32 (peak)** | — |
| 35° | 1.45 | 1.31 | 0.92 |
| 40° | **1.56 (peak)** | 1.09 | 0.97 |
| 45° | 1.41 | 0.77 | 1.00 |
| 50° | 1.06 | 0.59 | 0.95 |

- **0.1.1's problem:** its strake lift peaked at 40°, just past the 35° limit, so more strake meant a pitch-up between 35° and 45°. That capped it at ×2.
- **0.1.2's fix:** strake lift peaks near 32° and falls away past the limit, so the strake can be larger (×3) and the CG shift smaller (0.15 m).
- **Result:** the strakes trim the jet up to 35°, and the airframe is clearly nose-down-stable past it.

### Static, 300 km/h, ±30 elevators
| | Margin at 5° | 35°: trim / up / dn | 40°: up / dn | 45°: up / dn |
|---|---|---|---|---|
| 0.1.1 (38–60, ×2, CG 0.20) | +0.10 m | 28.7 / 6 / −45 | −19 / −44 | −44 / −47 |
| **0.1.2 (30–50, ×3, CG 0.15)** | **+0.13 m** | **28.4 / 8 / −44** | **−27 / −52** | **−61 / −64** |

### What did not work (bench): bigger strakes
Static numbers improve with ×4–×5 (trim at 35° down to 25°), but closed loop gets worse. A large strake moves too much of the pitch balance onto a surface whose lift falls steeply after breakdown, faster than ±30 elevators can follow while rolling.

| Curve / area / CG | Pulls | Loaded rolls (peak) | Push −30 + roll |
|---|---|---|---|
| 30–50 ×4 / 0.15 | 34.0–34.8 | 35.6–40.7 | **departs to −54** |
| 28–45 ×4 / 0.20 | capped at 24–35 by the FCS's authority limiter | 34.6–40.5 | −46.8 |
| 30–55 ×4 / 0.15 | 33.3–34.7 | 36.6–43.5 | ok |
| 28–45 ×5 / 0.15 | capped at 24–34 | 36.0–41.2 | ok |
| 30–50 ×3 / 0.20 | 34.6–34.9 | 36.7–40.7 | ok |
| **30–50 ×3 / 0.15 (0.1.2)** | **35.0 ± 0.04–0.12** | **35.1–35.3** | **−29.6** |
| 34–55 ×3 / 0.15 | 35.0 ± 0.05–0.13 | 35.3–35.5 | −29.6 |
| 30–50 ×2 / 0.20 | 35.0 (elevator at the stop at 450) | 35.1–35.2 | −29.7 |

## 2. No stick bypass of AoA protection without TVC
- **0.1.0 / 0.1.1 behaviour:** inherited the F-22E 2.44.0 rule. Full aft at the limit scaled away the limit's nose-down (1 − stick/0.9) and protection's push (1 − stick), so it became a zero-rate nose hold and the AoA rose as the flight path fell.
- **0.1.2:** the stick may do that only on a jet with pitch TVC (`tvc.max` > 0.5°, TVC not disabled), which the T/A-30 never has.
  - Protection's push and the capture bound's nose-down apply in full whatever the stick.
  - Forward stick asking for more nose-down than protection still gets it.
  - `FcsController.StickBypass`; the F-22E / Ifrit / Revoker code is untouched.
- **Bench, idle from 250 km/h, full aft held:**
  - 0.1.1 airframe: 37.5° (mushing past) → **35.1°**.
  - 0.1.2 airframe: **35.0 ± 0.14°**.

## Bench, shipped 0.1.2 defaults
| Case | Result |
|---|---|
| Full-aft pull 200 / 300 / 450 km/h | 35.0 ± 0.12 / 0.04 / 0.05, elevator 27.3 / 28.2 / 29.3 |
| Idle full aft 250 / 350 km/h | 35.0 ± 0.14 / 35.1 ± 0.05 |
| Loaded roll at the limit 250 / 350 / 450 | Peak 35.1 / 35.2 / 35.3; 28–37 °/s velocity-vector; sideslip ≤ 4.0° |
| Push + roll 200 / 300 km/h, full / idle | −30.2 … −30.4, sideslip ≤ 2.5° |
| Full roll 200 km/h, flaps down | Elevators avg −7.5°, never past 12.8° |
| Fuel 10 % / 100 % | Pulls 35.0; loaded rolls ≤ 35.7 / 35.4; idle holds 35.2 / 35.1 |
| 1 g rolls, reversals, pedals, engine out | Unchanged |

## Changes
| Setting | 0.1.1 | 0.1.2 |
|---|---|---|
| `StrakeBreakdownStart / End` (new) | 38 / 60 (fixed) | **30 / 50** |
| `StrakeResidualCN` (new) | 1.2 (fixed) | 1.2 |
| `LerxEffectiveness` | 2 | **3** |
| `CgShiftAft` | 0.20 | **0.15** |
| Stick bypass of AoA protection | yes (F-22E rule) | **only with pitch TVC (never on the Compass)** |

- **Config moves.** The player file keeps `CompassConfigRevision` (now 2); the dev file gets a new `CompassDevRevision`.
- **0.1.1 migration bug:** it checked the dev file for a key it only wrote to the player file, so it would have re-run every launch, resetting `CanardAttachedWindow` to off if you turned it on.
- **On your 0.1.1 dev file, once:** `LerxEffectiveness` 2 → 3, `CgShiftAft` 0.20 → 0.15. Values set by hand stay.

## Still open
- **Not flown.** The bench is uncalibrated for this jet.
- **Not benched:** structural limits, ground handling, landing and AI.
- **Unknown:** whether the game's chart remap and DownwashForce behave in game as on the bench. Watch the log lines below.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` (md5 `e08cb9c2716802e5c43fd8891024acd4`) + `meta.json` 0.1.2; 0.1.1 kept as `.dll.0.1.1.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.2.
- **`LogOutput.log` should show:**
  - "Compass FCS 0.1.2 defaults moved once: CgShiftAft 0.15 LerxEffectiveness 3".
  - "Compass FCS aero charts on trainer: 2 parts … strake vortex lift on, LERX area x3.00".
  - "CG shift: … = 0.150 m aft".
- **Release folder:** `C:\Games\NO mods\Compass-FCS-0.1.2-release`.
