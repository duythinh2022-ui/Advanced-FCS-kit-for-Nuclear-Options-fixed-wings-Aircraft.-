# F-22E Strike Raptor FCS 4.8.15 (Nuclear Option)

Base: 4.8.14. Bench only, not flown. Asked for (26 Nov): fix the no-TVC +55 overshoots without 4.8.14's side effects, and take back the costs of the first fix. Full study: `claude/F22E_capture_plan_fresh_check_2026-11-26.md`.

## What was wrong
- **The braking figure the capture planned with was 2-3× what the jet has near 55 deg.**
  - With every effector at its nose-down end, the swept model said 37-43 deg/s² at 50-56 deg; the jet got 14-18.
  - Near the limit the net nose-down is a small difference of two large moments: the airframe's nose-up against the stabs' nose-down.
  - The FCS's stab model is about 15 % stronger than the game's stabs. Best fit of the 10:34 log: 0.85.
  - That small error became a 2-3× error in the net.
- **The capture also planned the whole stop on the at-limit value plus a 30 % lean toward the present authority,** and it counted no time for the stabs to swing to their nose-down end.
- **4.8.14's roll share hid part of it,** but it also changed rolling pulls that never overshot.

## Changes (`FcsController`)
- **`CapturePlanFresh`** (dev, on). The capture plans its stop with:
  - the swept full-net nose-down, with the stabs at their measured strength (`CapturePlanStabStrength`, dev, 0.85: the plan only; the allocator, the roll limits and aDn are unchanged);
  - at the dynamic pressure the pull bleeds to on the way;
  - averaged over the AoA still to go (the jet brakes 2-3× harder at 45-50 deg than at 55);
  - less the roll's inertial pitch-up at the roll rate the coupling split allows at the limit.
- **The stop distance also takes the stabs' swing to their nose-down end** (half the slowest stab's transit at its servo rate), in their share of the braking. Before, only the nozzles' lag was counted, which is zero without TVC.
- **4.8.14's `CaptureRollShare` is retired.** Kept from 4.8.14: `LatReserveByNeed`, `CoupAtLimit`, `CoupExactSplit`, the 20 deg/s² floor removal.
- **`AoACaptureBrakeBlend` is not used** while `CapturePlanFresh` is on.
- **`CapturePlanFresh` false** = 4.8.14 without its roll share (bench: identical in all 57 windows).
- **New log columns:** `aBrakePlan`, `qbarRatio`, `planPath`, `aCoupArr`, `capStabTau`. No Rust core change.

## Bench
Your 10:34 log, every no-TVC approach past 40 deg (57), replayed from the logged state. 29.2 t; bench stabs 0.85 (the log's best fit).

### Overshoots past 55.5 (where bench 4.8.13 overshoots)

| CG | Fixed | Worst | Others changed > 1 deg |
|---|---|---|---|
| 0.0 (best fit) | 9 of 11 | 55.8 | 18 of 46 |
| −5 cm | 4 of 4 | 55.4 | 18 of 53 |
| +5 cm | 16 of 24 | 57.0 | 15 of 33 |

4.8.14 at the calibrated CG fixed 6 of 13 and changed 9 others.

### What it costs
- **Quick pulls released before the limit peak lower:** median −0.5 / −0.3 / −1.0 deg at the three CGs; 15-17 of 31-46 by more than 1 deg; worst −6 to −7.
  - The peak pitch rate is the same; the capture starts easing them from about 40 deg.
  - A fast pull that would overshoot and one you are about to release look identical until the release, so this trade can't be removed.
- **Pitch-rate curves:** no new hitches. The flagged re-rises are a second pull and a stick increase from 0.6 to 1.0.
- **Loaded roll at the AoA cap (pull then roll, 5-speed means):**

  | Case | 4.8.13 | 4.8.15 |
  |---|---|---|
  | No TVC, 36/45 cap | 140.4 / 95.2 | 142.2 / 97.5 |
  | TVC, 36/45 cap | 140.7 / 117.8 | 142.2 / 119.8 |

- **No-TVC battery against 4.8.13:**
  - reversal roll 30/36/45/50: 65.3/62.4/42.6/43.0 → 65.0/63.5/43.5/43.8
  - reversal sideslip: 2.1/2.4/6.2/4.7 → 1.9/2.4/6.4/4.8
  - push, pull, slow roll, half-stick roll: the same
- **TVC battery:** reversal and loaded roll the same. Single-run sideslip moves both ways (reversal at 36: 3.0 → 4.4; slow-roll pull at 36: 6.2 → 1.9).
- **Low-AoA roll-stop sideslip matrix:** worst 6.8 → 6.6, mean 3.5 → 3.4.

### What the bench can't tell
- The log can't separate stab strength from CG: 0.72 at −5 cm, 0.85 at 0, 1.0 at +5 cm all fit.
- If your jet's stabs are really at full strength, 4.8.15 plans about 15 % short of the braking it has: earlier, gentler stops, slightly lower quick-pull peaks.
- `CapturePlanStabStrength` in the dev file moves that: lower = earlier stops, higher = later stops and more overshoot.
- A flight log will show which.

## Going back
Reinstall `Aryx_F22E_StrikeRaptor_FCS.dll.4.8.14.bak`, or set `CapturePlanFresh = false` in the dev file (4.8.14 without its roll share).
