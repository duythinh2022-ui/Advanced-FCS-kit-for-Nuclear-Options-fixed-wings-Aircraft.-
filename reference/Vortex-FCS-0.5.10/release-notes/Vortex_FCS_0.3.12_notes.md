# Vortex FCS 0.3.12 — wing surfaces ±40°, rudders ±35°

17 Nov 2026. Tom, 17:09: "wings, do 40 and rudders do 35". Benched, not flown.

## Change

| Surface | Key | 0.3.11 | 0.3.12 |
|---|---|---|---|
| Inboard trailing edges (`elevator_L/R`, the stabilator kind on this jet) | `StabilatorRangeTEUp` / `StabilatorRangeTEDown` (Dev, 5. Airframe) | 30 / 30 | 40 / 40 |
| Outboard trailing edges (`aileron_L/R`) | `FlaperonRange` (Dev, 5. Airframe; capped at 40) | 30 | 40 |
| Rudders | `RudderRange` (not a key) | 30 | 35 |

- **Migration:** `VortexConfigRevision` 9. A Dev file still holding exactly 30 on any of the three keys is moved to 40 once. Values set by hand stay.
- **Within the game's own travel:** stock surface pitch range 40 + roll range 25; rudders 40 toe + 20 yaw.
- No allocator, law or native-core change. The ranges reach the solve through each effector's limits, and the native ABI is unchanged (2013001).

## Why

Static authority probe, clean jet at 83 m/s, surfaces only (the Vortex has no yaw vectoring in CTOL: game data pitch 10°, yaw 0°). The figure is the most velocity-vector roll acceleration the airframe can give with pitch held trimmed and no sideslip building up:

| Travel (wing / rudder) | +55° | +55°, 1 s into the roll | −55° |
|---|---|---|---|
| 30 / 30 | 18.3 deg/s² | 12.2 | 27.5 |
| 40 / 30 | 27.8 | 24.1 | 41.1 |
| 40 / 40 | 29.9 | 25.7 | 54.7 |

At +55° the binding constraint is pitch trim. The best solutions have the wing surfaces at their stops for nose-up trim and the pitch nozzle at or near its 10° limit.

## Bench (0.3.11 → 0.3.12)

**Max-AoA roll, full stick at the limit, 9 g limit, 250 / 300 / 360 km/h entry, both directions (means):**

| Case | Time to 20 deg/s VV roll | VV roll at 1 s | VV roll at 2 s | Max sideslip |
|---|---|---|---|---|
| +60° | 2.11 → 2.04 s | 12.9 → 10.8 | 20.5 → 20.6 | 3.6 → 4.8° |
| +55° | 2.02 → 1.85 s | 11.8 → 11.1 | 20.3 → 22.2 | 3.9 → 5.0° |
| −55° | 0.91 → 0.65 s | 24.4 → 33.7 | 38.8 → 45.0 | 3.3 → 3.1° |

**Pull-to-limit roll set (300 / 420 / 550 km/h):**
- +55°: time to 20 deg/s 1.23 → 1.26 s, VV roll at 1 s 17.7 → 19.7, sideslip 4.3 → 5.0°.
- +60°: 1.46 → 1.45 s, sideslip 4.1 → 5.5°.

**CTOL case set:**
- **Full-aft VV roll, 500 km/h (AoA 59°):** VV roll in the first 2 s 6.8 → 15.4 deg/s, peak 20.7 → 32.6. **Sideslip at roll onset 3.5 → 7.8°** (regression). The reversal starts from the higher rate, so the 5–7 s mean is −8.9 → −15.2.
- **VV 0.6 / 0.35:** roll within −8 / +1 %, sideslip 4.2 → 3.0° and 5.8 → 4.8°.
- **Loaded (1 760 kg one side), bank in 5 s:** 343 → 368 (left), 405 → 405 (right), 331 → 317 (left, 330 km/h), 1 g left 336 → 349. Sideslip within 0.2°.
- **Clean 1 g roll:** peak 202 → 201 deg/s.
- **Falling leaf:** yaw rate 34.7 → 40.6 deg/s. Stop 1.73 → 1.87 s. Reversal cross 1.93 → 1.80 s. Sideslip 4.6 → 5.5°.

**STOVL set:** hover, VL and transition identical. Decel's lowest altitude 259 → 257 m; VTO end distance within 1 %.

## Findings and open items

- **Regression:** sideslip at +AoA roll onset is up about 1° at the limit (4 → 5–5.5°) and 3.5 → 7.8° in the 500 km/h full-aft VV roll. More aileron travel gives more body-axis roll before the yaw catches up. The managed slip band (2 / 3 / 6) does not hold it to the 0.3.9 level.
- **+AoA gain is small** (~8 % on time to 20 deg/s at +55°), though the static ceiling roughly doubles.
- **Allocator dump, +55°, 0.7 s into the roll:**
  - It asks for about 4× the yaw it can get (target 215 kN·m, best about 50 kN·m).
  - It keeps the pitch nozzle at about 4°, where the best trimmed solution has it at 10°.
  - It is not the roll-time nozzle budget: `CanardPitchPriority` off and `RollPitchPriority` off change nothing, and `RollTvcBudget` 0 is much worse (time to 20 deg/s > 9 s).
  - Not fixed in 0.3.12.
- **Correction to the 16:59 chat:** the "flaperons 40" bench row only had the outboard surfaces at 40. The inboard trailing edges are the stabilator kind on this jet and stayed at 30. All four are at 40 here.
- **Bench-only tools added** (not in the mod):
  - `vprobe tables=1`: per-effector authority tables.
  - `vprobe dump=1`: per-part local AoA and moments.
  - `ALLOCDUMP=t`: allocator target, weights, commands and modelled moments.
  - `tools/authsearch.py`: trimmed ceiling search.
