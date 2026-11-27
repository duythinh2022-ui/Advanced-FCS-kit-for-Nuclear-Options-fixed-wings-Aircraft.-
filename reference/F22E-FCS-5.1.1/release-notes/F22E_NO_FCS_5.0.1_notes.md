# F-22E Strike Raptor FCS 5.0.1 (Nuclear Option)

Base: 5.0.0. **Bench-checked, installed, not flown.**

Asked for (26 Nov 20:39): "figure out another way to make the allocator bullet proof", then (21:12) "build it". Design and evidence: `claude/F22E_basin_screen_allocator_2026-11-26.md`.

## In short
- **New: the basin screen.** Dev key `BasinScreen`, default **on**. It lets the allocator get out of the stuck reverse-roll pose: both stabs rolling the jet the wrong way, held there against saturated flaperons.
- **Why the old allocator stayed stuck: it's a search failure.** At 396.8 s in your 20:10 log (TVC off):
  - The stabs gave −257 deg/s² of roll the wrong way, by the allocator's own model and by the bench plant posed at the logged state.
  - With the stabs mirrored and everything else re-solved, the same cost is **96× lower**.
  - No move of one or two surfaces gets there: the stabs, flaperons and rudders all have to move at once. That's why the 5.0.0 pair search hardly moved in flight.
- **New, off by default: `SlatOnePerSide`.** One slat effector per side, from the 20:25 bench.

## What the basin screen does
Each tick, after the main allocation:
1. **Skip check.** If the allocation already meets almost all of its target (leftover below the floor), nothing can be gained, so nothing runs. This test is exact.
2. **Cheap scan.** Every pair of stab positions on their tables (every 2nd point) more than 10° from where the stabs are now gets a score. The other surfaces answer it from their local slopes: a weighted least-squares move clipped to their travel, a 3×3 solve. Cost: a few µs.
3. **Real check.** If the best pair scores at least 2× better, the full allocator runs from it: stabs held there with everything else solved, then all freed.
4. **Accept or discard.** The result replaces the tick's only if a stab really ended up more than 10° from its old command (another branch, not the same one solved further), and the allocator's own cost is at least 30% and 0.25 lower.

Any tick where it doesn't switch is bit-identical to 5.0.0. The native core is unchanged (same `F22E_FcsCore.dll`); the screen calls the allocator, and the native and C# runs stay byte-identical on the bench with it on.

## Bench (two-zone world, 29.2 t, stab strength 0.85)
**Closed-loop reproduction of the stuck pose.** New tool `tools/grel.py`: full aft for 0.7 / 1.2 s, release, full right roll held 5 s, TVC off; 380 / 470 / 540 km/h; throttle 1 / 0.6.
- **With `TwoZoneStabClampPitchMargin` off** (your setting in the 20:10 log), the 540 km/h, 1.2 s, 60% case reproduces the log. 5.0.0 → 5.0.1:

  | | 5.0.0 | 5.0.1 |
  |---|---|---|
  | Rows in the reverse-roll pose | 126 | 0 |
  | Lowest AoA (deg) | −8.0 | +1.3 |
  | Peak sideslip (deg) | 4.1 | 2.5 |
  | Roll rate (deg/s) | 168.6 | 168.0 |

  The other 11 cases are within ±1%.
- **With the 4.8.17 rule on** (the default), no case got stuck on 5.0.0 either. The screen changes roll by 0 to −3% (worst 162.7 → 158.1 deg/s).
- **The 396 s window** replayed on the game's motion: the screen moves the stabs to the right branch (right +35°, left −12°) at 396.3 s, at 8° AoA.

**Full suite, 5.0.1 vs 5.0.0 (defaults).** The screen switches for a few ticks in most hard rolling manoeuvres, mostly at roll entry at 30–36° AoA. Peak sideslip:

| Group | Changed | Worse > 1° | Better > 1° | Mean |
|---|---|---|---|---|
| Reversals | 8 of 12 | 1 | 2 | −0.4° |
| Corkscrew entries | 14 of 20 | 1 | 0 | +0.2° |
| Regression set | 12 of 18 | 0 | 0 | −0.1° |

- **Worse:**
  - Pull-then-roll corkscrew entry, 400 km/h full thrust: 12.1 → 15.0.
  - Pull+roll reversal at 0.7 s, 450 km/h: 13.1 → 15.0.
  - Push-then-pull-through + roll, 350 km/h: yaw-stick metric 3.4 → 7.5.
- **Better:**
  - Full-aft roll reversal at 500 km/h: 11.7 → 7.0. This is the case where 5.0.0's pair search had made it 11.7.
  - Pull+roll reversal at 556 km/h: 10.9 → 9.6.
- **Unchanged:** utilisation pass/fail (same rows pass and fail). Damage suite within 0.1–2 deg, no new departures; the one existing departure (fins + R flaperon, 890 km/h roll reversal) is the same in 4.8.17 and 5.0.0.

**Battery (bat.py, 3 CGs).** With TVC on and TVC off at the 4.8.17 setting, slip changed by more than 1° in only these cases:
- **Worse:** rev30 CG 0 TVC off 1.9 → 3.0; rev36 CG −5 TVC on 3.3 → 5.0.
- **Better:** rev30 TVC on 5.8 → 2.5 and 5.4 → 3.7.

**CPU** (single-threaded bench, native allocator ~390 µs/tick):
- about 3 µs/tick in cruise;
- about 25 µs/tick in sustained rolls that can't make their rate;
- plus two allocator solves (~45 µs each, native) on the rare ticks it runs the real check.

## Dev keys

| Key | Section | Default | |
|---|---|---|---|
| `BasinScreen` | 4. Gains | **true** | false = 5.0.0. Takes effect at once |
| `SlatOnePerSide` | 5. Airframe | false | the three slats of a side as one effector. Bench: allocator time −24% native / −31% C#; roll −7…+8%; release sideslip mixed. Respawn after changing |

The startup log line now begins `F-22E 5.0.1 basin screen on, slats segmented; pair allocator off (4.8.17); …`.

Telemetry adds `basinSw` (screen switches since the last row) and `basinJ0` (its leftover score; 0 when it didn't run).

## Suggested A/B in flight (TVC off)
The entry from the logs: full aft about 0.7–1.2 s at 250–290 kt, release, full roll held about 5 s.
- **A:** `BasinScreen` true, `TwoZoneStabClampPitchMargin` false (screen instead of the clamp rule). On the bench this gives more roll in the repro: 168 vs 163 deg/s.
- **B:** `BasinScreen` false, `TwoZoneStabClampPitchMargin` false (5.0.0 as you flew it at 20:10).
- Look for the reversed stab split (left stab +, right stab −30 with right roll stick) and `basinSw` > 0 around it.

## Caveats
- **Bench only.** Nothing here has been flown.
- **The cost of the move itself is ignored.** A switch crosses 40–60° of stab travel in about 0.5 s; the 2× / 30% margins stand in for it.
- **Not judged on the 17:40 log.** There the bench's roll target has the opposite sign to the game's, so the 234 s pose can't be judged on it.
- **Stricter thresholds don't work.** With 50% and 1.0, the screen switches once and the repro gets stuck again.

## Files
- `F22E-StrikeRaptor-FCS-5.0.1.zip`: player zip (DLL + meta.json).
- `F22E-StrikeRaptor-FCS-5.0.1-Optional-Dev.zip`: dev unlock DLL, unchanged.
- `F22E_FCS_5.0.1_src.zip`, `F22E_FCS_5.0.1_src_bench_tools.zip`, `F22E_501_vs_500.diff`.
