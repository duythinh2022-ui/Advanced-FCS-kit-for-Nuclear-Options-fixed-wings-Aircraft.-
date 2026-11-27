# F-22E FCS 2.41.0: the allocator sees where the stabs will be, not where they are sent (the push-over hitch and twitch)

Base: 2.40.0. Compiled against the game's assemblies, bench-checked, **not flown**. 2.40.0 kept as `.dll.2.40.0.bak`.
CSV: one column appended after `ppQ`: `ppReach`, the number of allocations re-solved against the stabs' reach since the last row.

## Tom (4 Nov, 11:04 / 11:06)
2.40.0: "it is very hitchy and twitch, doesnt that show in your bench?" → "make it smoother".
- Yes, the bench showed it, for 2.35.3 and 2.40.0 alike. I had scored builds by their difference from 2.35.3, not on absolute smoothness.
- Scored on absolute smoothness from now on.

## The cause (11:01 log, 34.1 s)
- At 55° AoA the stab window opens.
  - The left stab, parked at −25° in deep stall (local AoA 69°), is sent to +35°.
  - That is a 0.45 s trip that passes through the stab's lift peak (local AoA 35 → 14°).
- **The allocator places every effector as if it were already at its command.**
  - It saw the stab at +35° (nose-up) and drove the nozzles further nose-down (−4 → −9°) to balance it.
  - Meanwhile the real stab, crossing its peak, gave its largest nose-down.
- **Result:**
  - Pitch rate −25 → −34 deg/s against −26.8 asked.
  - The same stab's roll moment pushed body roll 25 → 37 deg/s.
- **The game already has a device for this:** `ReleasedStickStabRateBound`, 0.1 s ("the nozzles and wing surfaces are never placed against a stabilator position that is still a third of a second away").
  - It acts only with the pitch stick released.
  - In a push-over the stick is pushed, so it was off.

## The change: the allocator's reach pass (`PushPlanReachTau` = 0.1 s, under a planned push)
- The allocator solves as before, so the stab's command (where it is going) stands.
- If a stab is commanded further than its servo reaches in 0.1 s, the solve runs again. Every other effector (nozzles, flaperons, rudders) is then placed against the stab's moment at the position it will actually reach in 0.1 s.
- The nozzles then see the peak's nose-down 0.1 s ahead, and unwind as it builds instead of winding the wrong way.
- **Why not simply bound the stab to its reach** (the existing device extended to the push):
  - The allocator then only sees the near side of the stab's travel. On that side the far end's roll and yaw benefit is invisible.
  - The stabs stayed symmetric, nobody braked the yaw, and released sideslip median went 5.0 → 8.2°. Tried as `PushPlanStabReach` (in source, off).

## Bench
**Hybrid replay**: pitch pinned to the log until the push starts, free from there. On unchanged builds it reproduces the game's overshoot.

**59 released pushes** (2.35.x–2.40.0 flights):

| | 2.40.0 | 2.41.0 |
|---|---|---|
| pitch overshoot, median / p90 (deg/s) | 6.8 / 12.9 | **2.8 / 10.5** (28 better by > 2, 1 worse) |
| pitch step (nose-down rate falling back mid-push), median / p90 | 3.9 / 11.5 | **1.3 / 5.8** |
| body-roll rise mid-push, median / p90 (deg/s) | 9.3 / 26.0 | **4.8 / 15.0** (38 better by > 3, 0 worse) |
| peak pitch acceleration after the window, median / p90 (deg/s²) | 84 / 125 | 69 / 112 |
| peak body-roll acceleration after the window, median / p90 (deg/s²) | 77 / 196 | 59 / 192 |
| **sideslip, median / p90 / max** | 5.0 / 8.7 / 12.0 | **7.4 / 11.9 / 14.9** (38 worse by > 1, 0 better) |
| time to 10° AoA, median / p90 | 1.42 / 1.86 s | 1.48 / 1.90 s |

- **The 3 released pushes of the 11:01 flight:**

  | push | overshoot | step | roll rise | sideslip |
  |---|---|---|---|---|
  | 33.6 s | 6.8 → 3.6 | 6.4 → 3.5 | 10.2 → 6.5 | 4.0 → 6.6 |
  | 48.3 s | 8.2 → 8.1 | 7.0 → 2.8 | 13.7 → 8.6 | 3.0 → 4.7 |
  | 68.2 s | 6.2 → 1.8 | 5.2 → 0.6 | 7.3 → 3.2 | 4.2 → 7.4 |

- **The sideslip cost is real.** Part of the old body-roll hump was the stab's transit roll pulse. Uncommanded, it happened to turn the remaining yaw into roll, which held the sideslip down. The allocator now counters that pulse like any other roll error.
- **Time to 10° AoA:** the commanded pitch rate is unchanged. The push gets there later because the overshoot that was speeding it up is gone.
- **13 held pushes:**
  - Overshoot 6.0 / 15.1 → 2.0 / 15.6.
  - Step 1.8 → 0.7.
  - Sideslip 3.3 / 9.6 / 15.4 → 3.3 / 8.6 / 12.2.
- **Free replays, 48-push set:**
  - Released sideslip 4.5 / 9.3 / 9.8 → 5.6 / 10.4 / 14.3.
  - Held 5.9 / 10.5 / 11.2 → 7.1 / 9.6 / 11.0.
  - Command dip identical.

**Suite vs 2.40.0:**
- Reversals, corkscrews, utilisation: identical.
- pull-then-pushover+roll 350 / 450: 4.9 → 5.3, 6.1 → 6.4.
- push-then-pullthrough+roll 350: 4.8 → 5.3.
- pullthrough released at zero 350: 6.0 → 6.1.
- Push-over released 560 / 600, sideslip after: 4.0 → 4.3, 2.5 → 2.9.
- HELD 560 / 600: 4.0 → 4.4, 3.3 → 4.1.

**Identity:** the default build replays the 59 + 13 hybrid pushes identically to the switch run it was benched as.

## Tried and not shipped (in source, off)
- **`PushPlanRelBudget`:** the released roll braked by the held stick's yaw-budget rule.
  - Brings the hybrid sideslip back to 5.8 / 10.0 / 12.1.
  - But it holds the roll while the AoA drains and stops it hard at the bottom. Suite push-over released 560 / 600: sideslip after the push 4.0 → 7.5, 2.5 → 8.5.
  - Velocity roll left at 10° AoA: p90 5.4, max 36 deg/s.
- **`PushPlanRelFollow`:** the released roll floored at the measured yaw's coordinated roll.
  - The roll keeps itself going: the yaw is there because the roll is.
  - Roll left at 10° AoA p90 19, max 45 deg/s.
- **Reach horizon 0.05 / 0.15 s:**
  - 0.05: smoother still (step 0.5) but sideslip 8.3 / 12.1 / 15.0.
  - 0.15: sideslip 7.0 / 11.3 / 14.9, step 1.9.

## Not verified
- **Not flown.**
- The hybrid replay mispredicted 2.37.0's roll-path change once. This change acts on the pitch path the hybrid reproduces, and on the allocator, which the bench runs as the game's own code.
- **The sideslip trade is the open question for the flight:** about +2.4° median, +3° at p90.

## Flown: 11:50 log (Tom, 11:53)
"the smoothness is acceptable and sideslips is higher but acceptable but seems like the push down is a little faster so which is a good trade."

**Released pushes from 65° AoA:**
- **84.1 s:** pitch rate on the command through the window (−16.8 … −18.7 against −18.3), peak 5 deg/s over at 32° AoA. Body roll steady 43 → 39 deg/s through the window, with no rise.
- **96.6 s:** −16.5 … −18.7 against −17.4, peak 3 deg/s over. Body roll 25 → 28 deg/s.
- **Sideslip:** 15.1° (84.1 s, entered at 73 deg/s of yaw) and 10.9° (96.6 s), against 2.5–3.2° on the 11:01 flight.
  - It builds late, from 35 to 10° AoA, as the body roll is braked to zero while the yaw is still 33 → 14 deg/s.
  - That is more than the bench's +2.4° median.
- `ppReach` 4–8 per row through the whole push: the reach pass runs on nearly every allocation.
