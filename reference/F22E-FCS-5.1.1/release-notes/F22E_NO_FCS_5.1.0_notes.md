# F-22E Strike Raptor FCS 5.1.0 (Nuclear Option)

Base: 5.0.2. **Bench-checked, installed, not flown.**

Asked for (26 Nov 21:45): "idk about the basin thing, it seems like the fix is not a very clean one. My goal is it is inherent into the allocator itself". Chose (AskUserQuestion): **horizon objective**.

## In short
- **The basin screen is gone.** The allocator itself now judges plans by their cost over the next second (dev key `HorizonObjective`, on; `HorizonTime` 1.0 s). It replaces the scoring of the 2.42 two-horizon pick. It's in C# and in the native core, byte-identical on the bench.
- **`TwoZoneStabClampPitchMargin` (the 4.8.17 no-TVC clamp rule) now defaults to off.** The horizon objective handles the stuck pose the rule was added for, and the no-TVC battery is better without it.
- **Same CPU** as 5.0.2: native allocator about 380–460 µs/tick either way (noise ±10%).

## Why the allocator got stuck
Every stuck case had the same setup:
- TVC off, pitch stick released, full roll held from a 40–55° pull;
- AoA bleeding about 10 deg/s (q held near zero while the lift turns the flight path into the body: nz·g/V);
- the pose starts at 8.6–11° body AoA, with roll rate still building.

At that point the down-going stab is at its lift peak (local AoA 22–25°) and the best pose flips to the other branch. The allocator stayed for two reasons:
1. **Every stage is local.** Escaping needs the stabs, flaperons and rudders all moving at once.
2. **The 2.42 two-horizon pick weighed the trip and the destination equally,** however long each lasts. A 0.4 s trip across the lift peak always lost to a pose that then stayed stuck for seconds. At 21:21 log, 25.0 s, it kept a pose costing 8.4 over one costing 0.006 once there.

## What the horizon objective does
When the reach model is active (pitch stick released, or a planned push), the allocator compares its plans by their cost over `HorizonTime`:
- **What each plan costs:**
  - its own cost once there;
  - plus, while the stabs are still travelling (in step, arriving together, at servo rate), the extra moment error of the trip, with the other surfaces answering from their local slopes, clipped to their travel.
- **The plans compared:**
  - the destination solve;
  - the reach plan (stabs only as far as their servos get in 0.1 s);
  - on the main solve only, the best pair of stab table points with everything else answering from its slopes. That pair is solved for real (stabs held there, then freed) only when it scores better over the horizon on the same linear model.
- **The cheapest over the horizon is commanded,** as before: stabs to their commands, everything else placed against where they'll be in 0.1 s.

There are no margins, floors or "far" tests; the only setting is the horizon length. Halfway across a switch the remaining trip is shorter, so it commits.

**Horizon length (bench):**
- 0.3 s stops the pose forming but can't get out once it's in.
- 0.5 s gets out of two of the three stuck states.
- 1.0 s gets out of all of them.

1.0 s is the default: about how long a stuck pose lasts.

## Bench
**Replays from the game's own state** (game roll command and stab and rudder positions, then flown freely, TVC off):

| From | 2.42 pick (5.0.0) | 5.1.0 |
|---|---|---|
| 21:21 log, 24.3 s (already stuck) | stuck: 92 rows, unmet roll up to 335, AoA −16.7, sideslip 12.8 | out: unmet up to 12, AoA −9.1, sideslip 5.6 |
| 21:36 log, 72.98 s (where 5.0.2's screen fired in flight) | stuck: unmet up to 491, AoA −12.2, sideslip 8.9 | never enters: unmet up to 92, AoA +2.2, sideslip 2.0, roll 196 |
| 20:10 log, 396.4 s | stuck: unmet up to 199, sideslip 6.0 | out: unmet up to 9, sideslip 5.7 |
| 21:36 log, 166.6 s (the screen-off stuck case) | stuck: unmet up to 347, AoA −14.6, sideslip 12.7 | out: unmet up to 5, AoA −6.0, sideslip 5.8 |

Unmet roll is in deg/s², AoA and sideslip in deg, roll in deg/s.

**Closed-loop reproduction** (`tools/grel.py`): no case gets stuck. The 540 km/h, 1.2 s, 60% case: sideslip 4.1 → 2.5, AoA min −8.0 → +1.7, roll 168.6 → 166.8 deg/s. The others are within ±1.5%.

**Battery** (bat.py, 3 CGs, 48 sideslip numbers each) against 5.0.0:
- **TVC off** (with the clamp rule off): worse by more than 1° in **0**, better in **8**; mean −0.37°; roll −3.1 … +3.9%. Best: push36 9.7 → 7.5 and 8.4 → 6.6.
- **TVC on:** worse in **3**, better in **7**; mean −0.37°; roll −6.0 … +6.6%.
  - Worse: pull36 CG −5 2.9 → 5.0; prolls45 CG −5 8.5 → 10.2.
  - Better: idle pull-release-roll 45° CG 0 10.2 → 2.4; rev30 5.8 → 2.7.

**Full suite against 5.0.0:**

| Group | Worse > 1° | Better > 1° | Mean |
|---|---|---|---|
| Reversals | 2 | 1 | +0.1° |
| Corkscrew entries | 0 | 1 | 0.0° |
| Regression set | 1 | 0 | +0.1° |

- **Worse:** pull+roll reversal at 450 km/h 13.1 → 14.8; full-aft roll reversal at 500 km/h 11.7 → 12.8; push-then-pull-through + roll 350 km/h 5.2 → 6.3.
- **Unchanged:** utilisation pass/fail (same count). Damage suite: the existing departure only.

**Native core:** ABI 5100000. Bench battery native vs C#: 96/96 CSVs byte-identical; reproduction and replays identical. The Windows DLL can't be run here; the in-game log line says whether it loaded.

## Dev keys

| Key | Section | Default | |
|---|---|---|---|
| `HorizonObjective` | 4. Gains | **true** | false = the 2.42 pick (5.0.0) |
| `HorizonTime` | 4. Gains | 1.0 s | 0.2–3 |
| `TwoZoneStabClampPitchMargin` | 5. Airframe | **false** (was true) | an existing cfg keeps its own value; yours was already false |
| `BasinScreen` | | removed | the line left in your cfg does nothing |
| `PairAllocator` | 4. Gains | false | set it **false** for testing: it was still on in your last two flights |
| `SlatOnePerSide` | 5. Airframe | false | unchanged |

The startup line now begins `F-22E 5.1.0 horizon objective on, 1.0 s, slats segmented; pair allocator off (4.8.17); …`.

Telemetry replaces `basinSw`, `basinJ0` with:
- `hzBranch`: stab branches taken;
- `hzRefine`: branch candidates solved;
- `hzPick`: reach plans picked.

All three count since the last row.

## Caveats
- **Bench only.** Nothing here has been flown.
- **The trip model is simple.** It assumes the stabs move in step and the other surfaces answer linearly from their slopes. The real allocator re-plans every tick, so this is an estimate of the trip, not a simulation of it.
- **The scan covers the two stabs only.** Other surfaces join only through their linear answer and the real solve that follows.
- **Behaviour changes everywhere the reach model is active,** with pitch released (see battery and suite), not only in the stuck cases.
