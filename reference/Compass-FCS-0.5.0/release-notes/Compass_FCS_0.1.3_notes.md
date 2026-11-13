# T/A-30 Compass FCS 0.1.3: slab tail curve, roll-coupling limit at any AoA, faster pitch reversals at the AoA limits

Base: 0.1.2. Patch bump. Compiled against the game; benched; **not flown**. 0.1.2 is kept as `.dll.0.1.2.bak`.

## Tom (5 Nov, 17:47)
"Check latest csv, sometimes both stabs pitch down, pitch up and down authority at the 2 ends of max aoa is slow"

## What the log shows (`Compass_FCS_20261105_174131.csv`)
The flight ran on the shipped 0.1.2 settings. Only `Telemetry` was changed.

### Both elevators nose-down (84.5–87.4 s)
- **Conditions:**
  - 70–75 m/s, 9.0 t (stores on), flaps out.
  - Stick centred, full roll.
  - Velocity-vector roll at 95–100 °/s with AoA at 9–11°.
- **The pitch axis ran out:**
  - Both elevators sat at −30 (nose-down stop) for 1.5 s.
  - The FCS's own estimate of nose-down authority left (`aDn`) was 5 deg/s², its floor.
- **The roll's inertial pitch-up won:**
  - (I<sub>zz</sub> − I<sub>xx</sub>)/I<sub>yy</sub> · p · r ≈ 0.94 · 1.7 · 0.37 rad/s² ≈ 34 deg/s².
  - Pitch rate rose 3 → 44 °/s and AoA went 10 → 46.5°, 11.5° past the limit.
- **Past the limit, protection had no slab force left.** At 40–46° the elevators were held at −8°: the slab was already past its peak lift.
- **The roll-coupling limit came on too late:**
  - It only engages from 12–22° AoA, so it started once AoA passed ~14°.
  - When it did, it allowed 50–75 °/s.
- **`elevator_R_cmd` chattered.** It flipped −30 / 0 / +20 frame to frame (124 jumps of more than 20° in the log): the allocator saw a flat force plateau and couldn't pick a deflection.

### Slow reversals at the two AoA limits
| | IAS | 20° of AoA off the limit |
|---|---|---|
| Push from +35 | 84 m/s | 1.08 s |
| Push from +35 | 54 m/s | 1.68 s |
| Pull from −29 | 82 m/s | 1.02 s |

- **Pushing from +35, the command shaping set the pace, not the slabs.**
  - The pitch-rate target ramped at ~30 deg/s²: 0.7 × the authority estimate, floor 30.
  - The elevators sat at a constant ~10° local AoA, not at the stop, with `aDn` at 41–53.
- **Pulling from −30, the slabs were truly saturated.** They held local AoA at −12 … −15°, right at peak lift.

### Root cause: the slabs fly the wing's airfoil
- **The game's `Trainer_stabilator` is a copy of the straight wing's `Trainer_wing`:** lift slope 5.0 /rad, CL 1.0 at 17° local AoA, dipping to 0.8 at 23°.
- **Where the tail sits:** it is ~4–5° below body AoA in the wing's downwash.
- **The effect:** at 15–25° body AoA the elevators are already near peak lift at neutral.
  - **Static map, 300 km/h, 20° AoA:** full nose-down gives −49 deg/s² against −51 at neutral, i.e. nothing past neutral.
  - **"Both stabs at −30"** therefore bought no extra moment.
  - **At ±35 / −30,** the authority is capped by the slab's CL 1.0.
- **Real tails are different.** A low-aspect-ratio swept tail slab (F/A-18, F-35, F-16) has a lower slope and stalls much later at a higher peak: roughly 3–3.5 /rad, peak CL ~1.2 at ~30°.

## Changes
| Item | 0.1.2 | 0.1.3 |
|---|---|---|
| `StabSlabFoil` (new) | — (stock foil) | **true**: elevators and fixed hstabs fly a swept-slab curve |
| `SlabStallAoA` / `SlabLiftSlope` (new) | — | **30 / 0.35**: lift peak at 30° local AoA, peak CL 1.22, slope 3.5 /rad |
| `LerxEffectiveness` | 3 | **3.4**: the slab's lower slope gives less nose-up at full trailing-edge-up; 3.4 puts the 35° trim back at 26–28° of the 30 |
| Roll-coupling limit without pitch TVC | only from 12–22° AoA, rising AoA or stick held at the limit; floor 20 deg/s² | **on from 3° AoA (full at 7°), whatever the stick; floor 12 deg/s²** (`CoupNoTvcAlways`) |
| Pitch-rate trajectory acceleration | 0.7 × authority estimate | **1.0 ×** (`PitchTrajAuthFrac`, Compass DLL only) |

### How the changes work
- **Slab curve:** this uses the existing chart-remap path, as the strakes do: a new `StallStagger` family `Slab` with a whole-curve stall map.
  - The game's aero job, the FCS's surface model and the downwash force all read the same table.
  - Symmetric on the negative side.
- **Roll-coupling limit:**
  - It caps the velocity-vector roll so the inertial pitch-up stays within half of the live nose-down authority at the present AoA and speed.
  - Where there is authority to spare, it doesn't bind: 1 g rolls are unchanged.
  - Only on jets without pitch TVC (`noPitchTvc`). The F-22E / Ifrit / Revoker DLLs are untouched.

### Config move
- **Dev file, once:** `LerxEffectiveness` 3 → 3.4 (`CompassDevRevision` 2 → 3); 1 or 2 also move.
- **Unchanged:**
  - A value set by hand to anything else stays.
  - `StabSlabFoil` is a new key, so it comes in at its default.

## Bench
- **Setup:** uncalibrated for this jet, so read for direction.
- **Roll cases:** 9.05 t (full fuel + 870 kg on the wings), flaps on the stock schedule.

### Static, 3000 m, 45 % fuel
| | 0.1.2 | 0.1.3 |
|---|---|---|
| Full nose-down at 35°, 200 / 300 / 450 km/h | −17 / −44 / −104 deg/s² | **−23 / −57 / −133** |
| Full nose-up at −30°, 200 / 300 / 450 km/h | 21 / 42 / 89 | **28 / 57 / 123** |
| 300 km/h, 20°: nose-down past neutral | none (−49 vs −51 at neutral) | **16 deg/s²** (−53 vs −37) |
| Elevator to trim 35°, 200 / 300 / 450 km/h | 27.3 / 28.4 / 28.9 | 25.9 / 27.6 / 28.3 |
| Static margin at 5° | +0.13 m | +0.05 m (still stable) |
| Can it trim 40°? | no | no (the airframe still pitches nose-down past the limit) |

### Reversals at the limits (full stick)
| Case | 0.1.2 | 0.1.3 |
|---|---|---|
| Push from +35, 300 km/h: 20° / 40° off, peak q | 1.53 s / 2.37 s, 19 °/s | **1.22 s / 1.90 s, 27 °/s** |
| Push from +35, 250 km/h | 1.82 s / 2.72 s, 13 °/s | **1.37 s / 2.08 s, 26 °/s** |
| Pull from −30, 300 km/h | 1.10 s / 1.72 s, 37 °/s | **0.90 s / 1.42 s, 48 °/s** |
| Pull from −30, 250 km/h | 1.23 s / 1.90 s, 28 °/s | **0.98 s / 1.55 s, 42 °/s** |
| Overshoot at the captures | +34.9 / −29.8 | same |

### Rolls with the stick centred after a pull (9 t, flaps out; the 84 s case)
| Case | Elevators avg | Time both at the nose-down stop | Peak AoA | Peak roll |
|---|---|---|---|---|
| 270 km/h, 0.1.2 | −22° | 38 % | 20.2 | 115 °/s |
| 270 km/h, **0.1.3** | **−13°** | **0 %** | 21.1 | 110 °/s |
| 240 km/h, 0.1.2 | −21° | 23 % | 25.1 | 103 °/s |
| 240 km/h, **0.1.3** | **−14°** | **13 %** (0.3 s, stopping the pitch rate left over from the pull) | 25.3 | 94 °/s |

- **Elevator chatter** (command jumps over 20° per frame) in those rolls: 42–46 → 2–3.
- **Each fix alone was not enough:**
  - The slab alone still left both elevators at the stop 51–59 % of the time.
  - The always-on coupling limit with the stock foil left 32 %.

### Unchanged within noise
| Case | 0.1.2 | 0.1.3 |
|---|---|---|
| Full-aft pulls 200 / 300 / 450 | 35.0 ± 0.12 / 0.04 / 0.05 | 35.0 ± 0.05 / 0.04 / 0.05 |
| Idle full aft 250 / 350 | 35.0 / 35.1 | 35.1 / 35.1 |
| Loaded rolls 250 / 350 / 450: AoA held | 31.9 / 33.5 / 32.2 | 33.3 / 34.1 / 32.6 |
| Loaded rolls: velocity-vector rate | 28 / 34 / 37 °/s | 26 / 30 / 34 °/s |
| Loaded rolls: sideslip | ≤ 4.0° | ≤ 3.8° |
| Push + roll 250: −30 hold | −23.0 ± 1.8 | **−27.7 ± 0.6** |
| 1 g rolls 300 / 500 / 700 | 142 / 207 / 248 °/s | 150 / 215 / 248 °/s |

- **Also unchanged:** reversals, pedals, release after a pull, engine out, fuel 10 % / 100 %.
- **AI on the stock FBW:** turn, climb and dive peaks within 0.5° of 0.1.2.

## Not done / still open
- **Not flown.**
- **The bench didn't reproduce the 46° runaway.**
  - In game the jet sat at 21° AoA at 1 g and 61 m/s, on the far side of the wing's lift dip.
  - At that speed the bench trims at ~8°, so its low-speed lift doesn't match the game's.
  - The fix targets the mechanism seen in the log (the coupling limit off below 14° AoA, the slab past its peak). Watch for the same case in the next log.
- **The flaps add nose-up at low speed.**
  - In this game, flaps are only extra lift on `wing1`, ahead of the shifted CG.
  - Bench, 240–270 km/h rolls: with the flaps held in, the elevators average −9 to −11° instead of −13 to −14, and the stop time at 240 km/h drops 13 → 3 %.
  - Not changed: the low-speed lift is useful. It could be scheduled off when nose-down margin runs low, or only with the gear down.
- **Low-AoA static margin is now +0.05 m.** Tighter, but still stable. The AI flies it as before on the bench.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` + `meta.json` 0.1.3; 0.1.2 kept as `.dll.0.1.2.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.3.
- **`LogOutput.log` should show:**
  - "Compass FCS 0.1.3 defaults moved once: LerxEffectiveness 3.4".
  - "... ready (... LERX x3.40 (vortex), tail slab curve, stall 30 deg ...)".
  - On spawn: "Compass FCS staggered stall / body lift: aero charts … (Strake …, Slab stall 30 deg -> chart …)".
