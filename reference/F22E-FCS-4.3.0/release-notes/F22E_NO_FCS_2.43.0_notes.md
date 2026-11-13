# F-22E FCS 2.43.0: the stab rate bound replaced (on 2.41.0)

Base: **2.41.0** (2.42.0 is dropped). Compiled against the game's assemblies, bench-checked, **not flown**. 2.42.0 kept as `.dll.2.42.0.bak`, 2.41.0 as `.dll.2.41.0.bak`. CSV unchanged
from 2.41.0 (`stabRateBound` now always 0; `ppReach` counts every reach re-solve, not only under a plan).

## Tom (4 Nov, 13:07)
"Then go back to .41 and remove 1 without tradeoffs". Item 1 is the stab rate bound. 2.42.0's way of replacing it (the reach pass at all times) improved
most cases but made several worse.

## The device and what replaces it
**`ReleasedStickStabRateBound`** (1.8.14 / 2.9.1):
- Clipped each stab command to 0.1 s of servo travel (relaxed by the stabs' stall share).
- Acted with the pitch stick released, or under the capped sweep's pitch hold.
- Its two jobs:
  - the nozzles are never placed against a stab position still 0.3 s away;
  - no stab is sent stop-to-stop while the other surfaces wait (2.9.1: the 75 s hitch).

**The replacement, in the same places, all model:**
1. **The reach pass (2.41.0).** The stab keeps its command. The other surfaces are placed against where it will be in 0.1 s. This does the first job.
2. **The two-horizon pick (`Allocator.TwoHorizon`, new).** When a stab is sent further than it gets in 0.1 s, two plans are scored:
   - the destination plan: the stab sent there, the rest placed against its reach;
   - the reach plan: the stab moved only as far as it gets in 0.1 s, everything solved together.

   Each plan's cost is counted at two horizons: over the stab's transit, and once the stab arrives. The destination plan's transit cost is the moment
   error averaged along the stab's path, from where it is to where it is sent. The cheaper plan is kept. This does the second job, in the allocator's
   own terms: a far destination wins only when it pays for its transit.
   - The bound was the reach plan, always. The 2.42.0 reach pass was the destination plan, always.
   - Without the transit term, a roll entry at −43° AoA (23:37 log, 50.6 s, held push) sent the right stab +2 → +35. The roll lagged its command
     by up to 140 deg/s² for 0.4 s; the pinned command dip went 2.8 → 5.8 deg/s and sideslip 5.7 → 7.4.
3. **Not changed:** under a planned push, the 2.41.0 reach pass is unchanged, so released and held pushes fly as 2.41.0. The two-horizon pick there made
   3 released pushes slower to 10° AoA. It stays as a bench switch, `TwoHorizonPlan`.
- The config entry stays and has no effect.

## Bench, 2.41.0 → 2.43.0
- **Hybrid replays (61 released, 14 held, your 11:50 flight included):**
  - Released: identical apart from one push (10° AoA 0.05 s later, overshoot and roll rise each lower on one push).
  - Held: sideslip median 4.3 → 4.1, overshoot p90 14.6 → 14.2. Nothing worse.
  - The 23:37 50.6 s held push: command dip 2.8 → 2.8, sideslip 5.7 → 4.6.
- **Free replays:**
  - 48-push set, released: identical.
  - 48-push set, held: sideslip p90 9.6 → 9.4, swing p90 13.6 → 12.9.
  - 54-push set: identical.
- **Suite, better:**
  - pull+roll reversal 450: 20.2 → 15.9
  - fullaft+roll+rev3s 320: 6.8 → 4.5
  - roll-then-pull-rev 400: 10.6 → 10.0
  - pull-then-pushover+roll 350: 5.3 → 4.7
  - one-fin push-over held 600: 7.0 → 4.9 (sideslip after 7.0 → 3.3)
- **Suite, worse:** one-fin push-over held 560, 4.2 → 6.0°.
  - This case is not stable on its own. The unchanged 2.41.0 law with the old bound's horizon set to 0.09 / 0.099 / 0.101 / 0.105 / 0.11 s instead of
    0.1 gives 5.0 / 4.2 / 5.2 / 5.7 / 4.5.
  - The governor (one fin: no plan) reads the capacity estimator, and that read moves with any change in the allocation.
  - 6.0 is 0.3° above that spread.
- **Everything else in the suite:** within 0.5° (reversals, corkscrews, utilisation, the other push / pull / damage cases).
- **Identity:** the default build replays the hybrid and free pushes identically to the switch run it was benched as, and its suite is identical.

## Also found (not changed)
- The capacity estimator's probes run the allocator, so they run the reach pass too: a probe then reads a stab's destination moment mixed with the
  other surfaces placed against its reach.
  - Taking the reach pass out of the probes (`ProbeNoReach`) moved the noise-sensitive damage cases both ways: no-fin push-over 560 went 17.7 → 20.0.
    Off.
  - This was already the case under a plan in 2.41.0.

## Not verified
- Not flown.
- In flight, with the pitch stick released or in the capped sweep's hold, the allocator now either sends a stab to where it is commanded (and places
  the rest against its reach), or does what the bound did. `ppReach` counts the re-solves.
