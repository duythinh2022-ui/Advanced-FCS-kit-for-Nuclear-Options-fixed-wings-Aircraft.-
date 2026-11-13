# T/A-30 Compass FCS 0.1.1: ±30° elevators, 35° AoA from vortex-lift strakes, roll fix, MPO velocity-vector roll

Base: 0.1.0. Patch bump (Compass airframe and defaults; no control-law change). Compiled against the game; benched; **not flown**.

## Tom (5 Nov, 16:32–16:44)
- "what it takes to make the compass do at least 35 without cranking up the range of motion of the stabs"
- "the stabs range of motion is too much, reduce it to +-30degs, and when i roll both of them point down"
- "also mpo velocety vector roll by default"
- On the CG / AoA choice: "U thought all moving stabs, with relaxed stability allowed for aoa past the stabs max deflection range, like the f-35 or the hornet?"

## Correction to 0.1.0's numbers
The 0.1.0 closed-loop AoA sweeps ran **without** the CG shift. The Revoker bench's `Defaults()` zeroes `CgShiftAft`, and the Compass branch didn't restore it. The static maps were right because they passed `CgShiftAft` explicitly; the closed-loop "max usable AoA 30" was measured on the unshifted jet. The game DLL always had the 0.27 shift. Fixed in the bench (it now restores `CgShiftAft` and `LerxEffectiveness`). On the real 0.1.0 airframe with ±40 elevators, 35° held with ~9° of elevator spare.

## Why both elevators went nose-down in rolls
It is **inertia coupling**, not a sign bug. A velocity-vector roll at low speed needs yaw rate as well; p·r pitches the nose up by (I<sub>zz</sub> − I<sub>xx</sub>)/I<sub>yy</sub> · p · r ≈ 0.94 × 1.5 × 0.27 rad/s² ≈ 22 deg/s² at 200 km/h (p 85, r 15 °/s).
- **With the 0.1.0 airframe at neutral stability,** nothing but the elevators resists that moment.
- **Below ~200 km/h with the flaps down,** a nose-down elevator past ~17° local AoA stalls, and both elevators sat at the stop.

Bench, full roll at 200 km/h with flaps down, ±30 elevators:

| Airframe | Elevators (avg, both) | At the stop |
|---|---|---|
| 0.1.0: CG 0.27, stock strakes | −25° | 59 % of the time |
| CG 0.15 | −8° | 0 % |
| **0.1.1: CG 0.20 + vortex strakes ×2** | **−8°** (max \|12\|) | **0 %** |

## Why the Compass couldn't trim high AoA, and the Hornet fix
**Per-part pitch moment** at 300 km/h, CG 0.27, surfaces neutral, 20–40° AoA:
- **Nose-up, roughly constant at about +57 deg/s²:** wing, strakes and forebody.
- **Nose-down:**
  - Elevators' own lift: −49 to −61.
  - Fixed hstabs: −16.
  - Aft body: −14.
  - Canted fins: −3 at 5° → −40 at 40°. The fins act partly as a horizontal tail at high AoA.

The strakes (`wingroot1`, 0.9 m² total) fly the wing's foil, so they stall at 17° with everything else.

**What the F/A-18 and F-35 do differently:** LEX vortex lift that keeps building to 35–40°+, so the airframe nearly trims itself up there and the all-moving tail only corrects.

**0.1.1 gives the strakes a slender-strake (Polhamus) vortex-lift curve** (`Strake.cs`):
- **Normal force:** C<sub>N</sub> = K<sub>p</sub>·sinα·cosα + K<sub>v</sub>·sin²α, with K<sub>p</sub> = 1.57 (aspect ratio ~1) and K<sub>v</sub> = π.
- **Vortex breakdown:** fades to a flat plate (1.2·sinα) from 38° to 60°.
- **Coefficients:** C<sub>L</sub> = C<sub>N</sub>·cosα, C<sub>D</sub> = 0.01 + C<sub>N</sub>·sinα.
- **Shape:** the low-AoA slope is 1.6 /rad (the foil's is 5.0), and C<sub>L</sub> reaches ~1.55 at 38–40°.
- **Effect:** more strake area adds nose-up mainly at high AoA. Low-AoA stability barely moves.
- **Plumbing:** it goes into the game's aero through the existing chart remap (new `StallStagger` family `Strake`), and the FCS's airframe model and the bench use the same curve.

### Static, 300 km/h, ±30 elevators (trim = elevator for zero pitch acceleration; up / dn = deg/s² left at full nose-up / nose-down)

| Airframe | Margin at 5° | 35°: trim / up / dn | 40°: up / dn |
|---|---|---|---|
| 0.1.0 (foil strakes, CG 0.27) | +0.02 m | > 30 / −5 / −55 | −34 / −59 |
| Foil strakes, CG 0.40 (CG alone) | −0.11 | 26.0 / 19 / −31 | — |
| Vortex ×4, CG 0.27 | −0.01 | < −30 / 52 / **+2** (can't push the nose down) | 33 / +8 |
| Vortex ×4, CG 0.15 | +0.11 | 24.1 / 28 / −23 | 7 / −18 |
| Vortex ×5, CG 0.05 | +0.19 | 25.3 / 23 / −30 | 2 / −23 |
| **Vortex ×2, CG 0.20 (0.1.1)** | **+0.10** | **28.7 / 6 / −45** | **−19 / −44** (can't trim 40: a natural ceiling) |

### Closed loop, 35° limit, ±30 elevators
| Airframe | Pulls 200–450 km/h | Loaded rolls (peak AoA) | Push −30 + roll |
|---|---|---|---|
| Vortex ×2 / CG 0.27 | 34.1–34.8 | **36–45** | ok |
| Vortex ×3 / CG 0.20 | 33.6–34.8 | **38–46** | **departs to −80** |
| Vortex ×4 / CG 0.15 | 30.5–34.6 | **38–49** | ok |
| Vortex ×5 / CG 0.05 | 34.5–35.0 | **36–46** | ok |
| **Vortex ×2 / CG 0.20** | **35.0 ± 0.05–0.11 (elevator 27.6–29.5)** | **35.0–35.3** | **−29.8, sideslip ≤ 2.6** |

- **Large strakes pitch the jet up between 35° and 45°** (vortex lift peaks at 38–40°). With ±30 elevators also rolling, the tail can't hold that: this is the Hornet's LEX pitch-up without the Hornet's flaps and tail authority.
- **×2 at CG 0.20 is the balance:**
  - Holds 35° with the elevators near their stop.
  - Past 35° the airframe pitches nose-down by itself, so the saturation is the safe kind.
  - The low-AoA margin of +0.10 m keeps the roll coupling off the elevators.
- **Fuel:** 10 % and 100 % give the same holds (35.0°).

## Changes
| Setting | 0.1.0 | 0.1.1 |
|---|---|---|
| `StabilatorRangeTEUp/Down` | 0 (stock ±40) | **30 / 30** |
| `AoALimitPositive / Negative` | 30 / 30 | **35 / 30** |
| `CgShiftAft` | 0.27 | **0.20** |
| `LerxEffectiveness` | 1 | **2** (area) |
| `StrakeVortexLift` (new) | — | **true** |
| `CanardAttachedWindow` | on (Revoker) | **off**. It walked the stabilators toward the foil knee above 30° AoA with roll input, holding the Compass's elevators nose-up while protection needed nose-down. The Compass foil keeps CL 0.8–1.0 past stall, so the window threw authority away. (It wasn't the cause of the strake overshoot; same results with it off.) |
| `MpoRollMode` | SeparateRollYaw | **VelocityVectorRoll** |

- **One-time move for files written by 0.1.0** (new `CompassConfigRevision` key in the player cfg):
  - `MpoRollMode` SeparateRollYaw → VelocityVectorRoll.
  - Dev file: stab travel 0 → 30, AoA 30 → 35, CG 0.27 → 0.20, LERX 1 → 2, CanardAttachedWindow → off.
  - Values set by hand to anything else stay.

## Bench, shipped defaults
| Case | Result |
|---|---|
| Full-aft pull 200 / 300 / 450 km/h | 35.0 ± 0.11 / 0.04 / 0.05, elevator 27.6 / 28.5 / 29.5 |
| Loaded roll at the limit 250 / 350 / 450 | peak 35.0 / 35.1 / 35.3, sideslip ≤ 3.6°, 29–37 °/s velocity-vector |
| Full push + roll 200 / 250 km/h (full / idle) | −30.2, sideslip ≤ 2.6° |
| Full roll 200 km/h, flaps down | elevators avg −8°, never past 12° |
| 1 g rolls 300 / 500 / 700 km/h | 142 / 208 / 248 °/s; sideslip ≤ 1.7° (unchanged) |
| Engine out 250 km/h | 22° cap, sideslip 0.3° (unchanged) |
| Idle from 250 km/h, full aft held | AoA mushes to 37.5° (zero-rate nose hold, F-22E 2.44 rule); protection pushes when released |

## Still open
- **Not flown.** The bench is uncalibrated for the Compass.
- **Low-thrust AoA cap:** the inherited cap (meant for TVC jets at idle) can drop the limit toward its 20° floor near stall speed at idle. Thrust adds no pitch authority on the Compass. Seen on the 0.1.0 airframe; not on 0.1.1's in the same case.
- **Not benched:** structural limits, ground handling, landing, AI.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` (md5 `54c5263518ba6341c1235573280b52c0`) + `meta.json` 0.1.1; 0.1.0 kept as `.dll.0.1.0.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.1.
- **`LogOutput.log` should show:**
  - "Compass FCS 0.1.1 defaults moved once: …".
  - "Compass FCS aero charts on trainer: 2 parts … strake vortex lift on, LERX area x2.00".
  - "elevator_X:Stabilator ±30".
- **Release folder:** `C:\Games\NO mods\Compass-FCS-0.1.1-release` (main zip, Optional DevUnlock, Source with the bench, notes, `meta.json`).
