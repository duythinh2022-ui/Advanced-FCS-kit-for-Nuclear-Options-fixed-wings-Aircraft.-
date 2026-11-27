# T/A-30 Compass FCS 0.1.10: the inner flaps no longer move with the pitch stick

Base: 0.1.9. Patch bump: internal change only; no config entries changed. Compiled; benched; **not flown**. 0.1.9 is kept as `.dll.0.1.9.bak`.

## Tom (5 Nov, 21:45 and 22:02)
- "make the inner flaps on compass not respond to pitch input"
- "when i'm taxi-ing, pitch inputs also effect flaps"

## Why they moved
**In the air:** since the Revoker port (0.2.0 `FlapFlaperons`), `flap_L/R` have been allocator effectors for pitch as well as roll.
- Their common deflection was held to the flap schedule only by a pair cost (`ElevonSymCost` 2).
- So the allocator used it for pitch whenever that was cheaper than the elevators. On the bench, full push then full pull at 250 km/h, the pair's common deflection went −20 → +19°.

**On the ground:** the ground law moved them with the pitch stick (`GroundElevonPitch`, full aft = trailing edge up). It is a Revoker setting: the canard delta's trailing edges are its pitch surfaces.

## Changes (`FlapPitchLock`)
1. **Ground:** the pitch-stick throw on the inner flaps is gone. Roll, the landing droop and the rollout brake set are unchanged.
2. **The common part is held to the schedule.** The schedule is the landing droop, the airbrake set and the rollout brake set.
   - The allocator prices any departure from it 200× harder (`FlapPitchLockCostK`).
   - After the solve, the pair's command is projected onto the schedule exactly. Only the differential is left, and it is kept as large as fits.
3. **The flaps' pitch column is held at its live value**, as the F-22E does for its ailerons (`noPitch`). So no flap deflection reads as pitch to the allocator.
   - **Why this was needed:** with only the common part priced, it still found pitch in the two flaps' unequal curves.
   - **Bench, push at 250 km/h:** the right flap went 16 → 2° trailing-edge down with the left on its stop. That is a 9° "differential" with no roll asked.
4. **The droop gives way to the differential (`flapDiffYield`).**
   - **The problem:** with the common part held at the droop, the down-going flap hits its stop early. At 12° of droop and 20° of travel, only 8° of differential is left. 0.1.9 got around this by raising the common part.
   - **Now:** the droop comes off, as `FlapRollBias` already does for the stick. It retracts far enough that the differential the allocator asks for has 3° to spare.
   - Out at 3 /s, back at 1.5 /s. Lateral demand only.
5. **With a tail slab shot off, the lock lets go** (`FlapPitchLockStabRelease`). The flaps then work as in 0.1.9.
   - **Why:** the flaps' common part is then part of the jet's last nose-down.
   - **Bench, left outer wing + right stab, 1 g → full aft → roll reversal at 470 km/h:** locked, AoA 43° and departs; free, 7.5°.
   - **Locked, the same case at 440–510 km/h:** AoA 0.2–0.6° higher, with one more departure at 470 and a 13° overshoot at 490.

**New telemetry columns:** `flapYield` (0–1) and `flapSymTgt` (deg; the scheduled common deflection).

## Bench
### Clean jet: full push → full pull, rolls, rolls under a push, slow pushes (250–600 km/h)
**Common deflection, against the schedule:**
- 0.1.9: up to 28° off it.
- **0.1.10: within 0.4°**, i.e. the actuator lag behind a moving droop.
- Roll cases: within 13°, which is `FlapRollBias` taking the droop off, as before.

**Differential with no roll asked:** 0.0–1.7° (0.1.9: up to 12.5°).

| | 0.1.9 | **0.1.10** |
|---|---|---|
| Rolls at 1 g, 200–600 km/h | | identical (±1 °/s, ±1° bank) |
| Push onset, 250 km/h: peak nose-down rate | −48 °/s | −43 °/s |
| Push onset, 350 km/h: peak nose-down rate | −34.5 °/s | −27.6 °/s |
| Pull out of a −29° push, 250 / 300 km/h: peak nose-up rate | 41.9 / 47.9 °/s | 37.9 / 44.7 °/s |
| Pull out of a −29° push: time to +20° of AoA change | | +0.02 s |
| Pull out of a −29° push: AoA reached | | the same |
| Push out of +35°, 250 / 300 km/h | | identical |
| Roll under a 0.6 push at 250 km/h: peak roll rate | 96 °/s | 82 °/s |
| Roll at max AoA, 350 km/h: velocity-vector roll | 31 °/s | 29 °/s |

**The cost:** less pitch rate at the very start of a push, and in a hard pull out of one, because the flaps no longer add to the elevators there.

### Damaged jet (133-case matrix, mid-flight damage, pushes)
| Case | 0.1.9 | **0.1.10** |
|---|---|---|
| 133-case matrix: departs | 1 | 1 (the same whole-wing case) |
| Outer wing gone, with or without the fin: AoA overshoot | up to 14.4° | 12.4° |
| Outer wing gone, with or without the fin: peak sideslip | 7.1° | 3.8° |
| Stab-loss sets | | identical (lock released) |
| Mid-flight damage sequence | | within 2° AoA, no departures |
| Negative-AoA pushes (36) | | within 0.9° AoA |

### Not benched
**Taxiing.** The bench has no ground model; this is the one-line ground-law change above.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` + `meta.json` 0.1.10; 0.1.9 kept as `.dll.0.1.9.bak`.
- **Dev unlock:** `plugins\Compass_TA30_FCS_Dev.dll` 0.1.10.
- **Release:** `C:\Games\NO mods\Compass-FCS-0.1.10-release`.
