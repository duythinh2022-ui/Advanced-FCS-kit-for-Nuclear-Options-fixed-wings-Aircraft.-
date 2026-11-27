# T/A-30 Compass FCS 0.1.4: low-AoA roll-rate ceiling raised

Base: 0.1.3. Patch bump: three dev-setting defaults, no law change. Compiled; benched; **not flown**. 0.1.3 is kept as `.dll.0.1.3.bak`.

## Tom (5 Nov, 18:15)
"the roll rate is so bad maybe it is the airframe's problem?. Also many times the roll doesnt respond in an instant"

## What the 18:12 log shows (0.1.3, clean jet, 7.0 t, 150–455 km/h)
Full-stick rolls (|stick| > 0.9), median of each speed / AoA bin:

| AoA | IAS | Roll rate | What capped it |
|---|---|---|---|
| < 10° | < 250 km/h | 80 °/s | FCS roll ceiling (`pMax`) 59 |
| < 10° | 250–330 | 107 | `pMax` 103 |
| < 10° | 330–400 | 135 | `pMax` 127 |
| < 10° | 400–500 | 158 | `pMax` 144 |
| 10–20° | 250–400 | 82–114 | Roll-coupling limit 77–96 (0.1.3) |
| 30–36° | 180–400 | 23–43 | Sideslip-margin rate cap 14–33, then the airframe |

### Low AoA: the FCS, not the airframe
- **The evidence:** a full-stick roll at 300 km/h held 105–108 °/s with the ailerons at 6–7° of their 30 (126 s).
- **The cause is the ceiling** `pMax = min(F-22E stock reference × 1.2, roll authority ÷ modelled roll damping)`:
  - The F-22E stock reference is the "original aircraft" curve inherited from the F-22E port. It is not the Compass's: the Compass's stock fly-by-wire allows 8 rad/s.
  - The damping estimate under-reads this jet by ~2×.
- **The airframe can do much more.** Bench, ceiling removed: 180 °/s at 200 km/h, 320 at 300 km/h, ~400 above 450, with the ailerons under 30° and sideslip ≤ 2.5°.

### Mid AoA (10–20°): a deliberate limit
The 0.1.3 roll-coupling limit binds here at low speed: it holds the roll's inertial pitch-up to half of the tail's nose-down authority.

| `CouplingRollLimit` | 300 km/h roll at 10° AoA | 240–270 km/h rolls after a pull: both elevators at the stop |
|---|---|---|
| 0.5 (kept) | 112 °/s | 0–7 % |
| 0.7 | 132 °/s | 21 % |

The two outcomes trade against each other, so 0.5 stays.

### High AoA (25–35°): mostly the airframe
- **Ailerons:** the straight `Trainer_wing` stalls at 17° local AoA. The FCS limits each aileron to the side that still works, ~12–14°.
- **Rudders:** both at the stop.
- **Yaw authority:** `aYaw` 70–110 deg/s², against 150–400 for roll.
- **Why yaw matters:**
  - A roll about the velocity vector at 30° needs a yaw rate of half the roll rate.
  - So the onset waits for the canted fins to build yaw. At 97 s (30° AoA, 310 km/h) the ailerons were held at 4–8° for 0.45 s while the rudder led.
- **Sideslip margin:** the rate cap keeps a roll reversal inside `RollStopSideslip` 6°.
- **Relaxing the governor doesn't buy much.** Bench, at 35° AoA:
  - `RollStopSideslip` 10 → 29–33 °/s.
  - 20 → 37 °/s.
  - The 0.1.3 baseline is 25–31.

### "Doesn't respond in an instant"
| Case | Time to 10 °/s | Time to 30 °/s |
|---|---|---|
| Low AoA, 300 km/h | 0.05–0.08 s | 0.10–0.13 s |
| Low AoA, ~200 km/h | 0.10–0.15 s | 0.2–0.37 s |
| High AoA with full aft | 0.25–0.48 s | 0.5–0.9 s |

- **Low AoA:** the response tracks the command closely, so the felt lag there is mostly the low ceiling.
- **High AoA:** this is the slow case: the yaw has to build first, and the aileron is on a stalled wing.

## Changes (dev file, moved once if they hold the old defaults; `CompassDevRevision` 3 → 4)
| Setting | 0.1.3 | 0.1.4 |
|---|---|---|
| `RollRateGainLowIAS` | 1.2 | **2.0** |
| `RollRateGainHighIAS` | 1.1 | **1.8** |
| `RollAuthorityUse` | 1 | **2** |
| `MaxRollRate` | 250 | 250 (unchanged) |

## Bench (1 g full roll, 1500 m)
| IAS | 0.1.3 | 0.1.4 | Ailerons (median / max) | Sideslip |
|---|---|---|---|---|
| 200 km/h | 107 °/s | **180** | 18 / 24° | 2.5° |
| 300 | 142 | **247** | 16 / 21 | 2.0 |
| 450 | 188 | **248** (ceiling) | 14 / 15 | 1.4 |
| 600 | 226 | **248** | 10 / 11 | 1.0 |
| Roll at 10° AoA, 400 km/h | 166 | **208** | | |

- **Unchanged:**
  - Time to 30 °/s: 0.08–0.22 s.
  - 35° holds, loaded rolls, pushes, AoA-limit reversals, pedals, engine out.
  - Mid-AoA rolls at 250–300 km/h: limited by the coupling limit, as above.
- **Cost: longer roll stops.** From 247 °/s the stop takes 0.73 s and 67° of bank after release, against 0.52 s / 28° from 0.1.3's 142. At these rates the stop is roll deceleration, not lag. To trade some rate back for crisper stops, lower `RollRateGainLowIAS` / `HighIAS` (1.7 / 1.5 gives ~210 °/s at 300 km/h).

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` + `meta.json` 0.1.4; 0.1.3 kept as `.dll.0.1.3.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.4.
- **`LogOutput.log` should show** "Compass FCS 0.1.4 defaults moved once: RollRateGainLowIAS 2.0 RollRateGainHighIAS 1.8 RollAuthorityUse 2".
