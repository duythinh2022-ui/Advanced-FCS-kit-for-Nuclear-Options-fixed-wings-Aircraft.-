# Vortex FCS 0.3.13 — wrong-way roll at max AoA fixed (pair pass migrated on), pitch nozzle ±15°

17 Nov 2026. Tom, 19:05: "check towards the end of the log, i commanded right but the jet went left. Fix that, also set 15deg as the nozzle deflection, migrate settings". Benched, not flown.

## The 19:01 log, 97–116 s

Full aft and full right stick from 97.3 s, AoA at the 60° limit from 100.7 s, 70–83 m/s IAS, afterburner (T/W 1.21), 12 690 kg.

**What happened:**
- 99–100 s: the roll starts right (velocity-vector roll 39 deg/s at 99 s).
- From 100.7 s: yaw saturated (`yawSat` 1.0) and the allocator's unmet roll / yaw shortfall went to 45–96 / 80–170 deg/s².
- The surfaces then froze for 15 s:
  - rudders +35 / −35;
  - inboard trailing edges +27 / +40;
  - outboard −7 / +8;
  - pitch nozzle +6–8°.
- **Direction reversed.** Velocity-vector roll fell 25 → 0 deg/s by 105.5 s, then ran **left** to −16 deg/s by 115 s with the stick still full right. Sideslip grew 3 → 9°.

**Not an aerodynamics mismatch.** The bench equation-error check at the logged poses and surface positions predicts the same left yaw and roll accelerations the log shows (yaw acceleration −7 → −0.5 deg/s², log ≈ −2.8 mean). With that surface set, the airframe does go left.

**Cause:** the allocator sat in the reversed rudder set, the local minimum the 0.3.9 pair pass was built to leave (0.3.8 notes, 15:36 log). In Tom's Dev file the pair pass was off:
- `AllocatorPairPass` was off by default in 0.3.7–0.3.8 and on by default from 0.3.9.
- No migration moved a file still holding `false`. So since 0.3.9, the high-AoA roll fix never ran in this file.
- It is the only setting in the file that differs from the current defaults, apart from `TvcPitchRange` 15, which Tom set by hand. Checked by regenerating the bench stub from his file.

**Bench replays of the log window (Tom's file):**

| Replay from | Pair pass off (as in game): VV roll +1 / +3 / +6 s | Pair pass on (0.3.13) |
|---|---|---|
| 100.0 s | 35 / 40 / 39 deg/s; rudders still +35 / −35 at +1 s | 47 / 53 / 39; rudders out of the set (+9 / −14) within 1 s |
| 100.5 s | 32 / 25 / **13** (decaying toward the game's reversal) | 44 / 50 / 37; rudders −11 / +2 |
| 101.0 s | 30 / 34 / 41 | 40 / 46 / 45 |

The bench did not reproduce the full 15 s reversal from 97 s with either setting: the bench and game trajectories split before 100 s. The replays from inside the stuck set do show the pair pass leaving it within a second, and the solve without it staying there.

## Changes

- **`AllocatorPairPass` migrated on.** `VortexConfigRevision` 10: a Dev file holding `false` is set to `true` once. The default was already `true`.
- **`TvcPitchRange` 0 → 15°.** A file still holding 0 (meaning the game's 10°) is moved to 15 once; 15 is the new default. The game's nozzle clamps its angle at 20°, so 15° moves the thrust and the nozzle mesh without reaching that clamp. Visual clearance of the mesh is not checked. Takes effect on the next spawn.
- No law, allocator or native-core change; ABI 2013001.

## Bench (0.3.12 → 0.3.13; the bench already ran the pair pass, so this column is the nozzle range)

**Roll at the AoA limit** (9 g, 250 / 300 / 360 km/h entry, both directions):

| Case | Time to 20 deg/s | VV roll at 2 s | Max sideslip |
|---|---|---|---|
| +60° | 2.04 → 1.93 s | 20.6 → 22.4 deg/s | 4.8 → 5.0° |
| +55° | 1.85 → 1.93 s | 22.2 → 21.1 | 5.0 → 5.2° |
| −55° | 0.65 → 0.61 s | 45.0 → 57.9 | 3.1 → 3.6° |

**CTOL case set:**
- **Loaded rolls (1 760 kg one side), bank in 5 s:** 368 → 372, 405 → 419, 317 → **400** (330 km/h; sideslip 1.5 → 3.5°), 1 g 349 → 349.
- **Clean 1 g roll:** 201 → 201 deg/s.
- **500 km/h full-aft VV roll:** 15.4 → 14.8 deg/s, **sideslip 7.8 → 9.6° (regression)**.
- **VV 0.35 / 0.6:** within ±3 % in roll; sideslip 4.8 → 5.9° / 3.0 → 3.3°.
- **Falling leaf:** stop 1.87 → 2.02 s, reversal 1.80 → 1.72 s, sideslip 5.5 → 5.2°.

**Mid-AoA rolls** (1 g and pulling, 300–550 km/h, part throttle and afterburner): bank in 1.5 s within ±3°.

**STOVL set:** identical.

## Open

- The 500 km/h full-aft sideslip spike: with `TvcNeutralHold` 0 as well (bench, 18:40), it is 6.0° instead of 9.6°, at a 0.2 s slower leaf stop. Not changed here.
- The entry into the stuck set (game vs bench split before 100 s) is not reproduced.
