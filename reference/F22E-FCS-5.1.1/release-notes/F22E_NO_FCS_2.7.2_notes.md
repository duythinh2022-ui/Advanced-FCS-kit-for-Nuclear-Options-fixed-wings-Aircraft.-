# F-22E FCS 2.7.2 — the rate cap is back, on the whole airframe's capacity

Base: 2.7.1. **Bench-only, not flown.** 2.7.1 kept as `.dll.2.7.1.bak`.

Tom, 10:19: *"the roll rate is much faster than my 41 % slider. I asked for the reference governor to not only do it based on dedicated active yaw effectors like rudders or the ruddervators on the Ifrit, but also the indirect ones like the passive one, or the roll TVC on the Ifrit as AoA increases since it can suppress sideslip. And yet you just let the jet crank the roll rate all the way at those mid AoA, then slow down the acceleration and deceleration for sideslip control."*

That is the correct reading of the spec and 2.7.0 / 2.7.1 got it backwards: they removed the cap on the steady rate (so mid-AoA rolls ran at the airframe limit, 244 deg/s at 413 kt / 15°) and then paced the reversal — a slowed deceleration by any other name. The rate cap was never the problem; its rudder-only estimate was.

## 2.7.2
- **The rate cap (`RollPaceRateBlend`, your 0.41) is back** and is the governor's lever: it models a full-stick reversal from the present rate at the tuned acceleration and caps the commanded rate so that reversal lands on `RollStopSideslip`. Its capacity is now **the whole airframe's**: every effector's yaw still available toward the reversal's side from where the allocator has it (rudders, the stabs' differential, the Ifrit's roll TVC when ported — anything the allocator can turn to yaw), less what the allocator reports it cannot deliver (`unmetZ`), **plus the airframe's directional stiffness at the margin sideslip** (the probe's restoring moment × margin: the passive share, the airframe balancing laterally at that AoA). Its dead time is the yaw-weighted servo travel of those effectors to the deflection the reversal needs. Telemetry `rcAOpp` (deg/s²), `rcTc` (s), `twoAxisCap` (the cap, deg/s).
- **Nothing paces the trajectory.** 2.7.1's target rate-limit is off (`PaceRateLimitOn`), the open-loop acceleration cap is off, starts / stops / reversals run at the tuned acceleration and braking.
- **The window holds the target only on measured saturation:** a latch on the allocator's unmet report relative to the axis's authority (on at 12.5 % of `aYaw` unmet, off at 6 %; at 200 km/h that is 3 deg/s², at 400 kt 30), never on rudder load, never a blend toward the raw target. With the margin lead of 2.7.1. This is what catches push + roll at 200 km/h (7° bench; 17° with no window at all, 3.4° in 2.6.11).
- **The cap's share fades with the rudders** (`rudderAuth`, as 2.6.9): past their fade the yaw is the velocity roll and the one-axis law's braking owns it. No falling-leaf tax.

## Bench (2× pessimistic on sideslip)
| | 2.6.11 | 2.7.2 |
|---|---|---|
| reversals 450 / 556 / 650 km/h, 11–13° | 0.23 / 0.23 / 0.28 s; 5.8 / 6.5 / 3.9° | 0.22 / 0.20 / 0.22 s; 6.6 / 5.5 / 4.4° |
| half pull 400 km/h, 29° | from 63: 0.25 s, 5.5° | from 80: 0.60 s, 8.1° |
| half pull 556 / 700 km/h | 0.38 / 0.33 s; 5.8 / 4.1° | 0.25 / 0.32 s; 7.1 / 4.5° |
| full aft 300 / 500 km/h | 1.53 / 0.40 s; 5.4 / 6.1° | 0.87 / 0.70 s; 4.0 / 6.7° |
| falling leaf 160 / 300 km/h | 2.28 / 1.78 s; new peak 47 / 50 | 1.03 / 1.68 s; 45 / 50 |
| utilisation invariant | 6 FAIL / 10 (incl. the falling leaf at 72–73 %) | 4 FAIL / 9: 55–71 % at 15–55° AoA (the cap's share), **falling leaf 96–99 %** |
| corkscrew entry 556 / 630 / 700 km/h, dip | 37 / 63 / 90 | 41 / 64 / 93 |
| push + roll 200 / 300 km/h | 3.4 / 2.6° | 7.2 / 7.1° |
| pull-then-roll 250 / 320 / 400 | 4.6 / 3.7 / 4.2° | 4.5 / 4.2 / 4.3° |

## The corkscrew-entry dip, plainly
It is the same as 2.6.11 on the bench, and the cap's own telemetry says why: at 25° AoA the capacity is ample (`rcAOpp` 250–310 deg/s² airframe-wide) but the **dead time** `rcTc` is 0.4 s — the servos at 100 deg/s have to cross most of ±35° to reverse the yaw. The sideslip of a reversal is about (yaw swing) × (dead time) × cos AoA: a reversal from 100 deg/s at 25° swings the yaw 84 deg/s, so a 4° margin allows roughly 84 × τ ≤ 4.4 → τ ≤ 0.05 s. No authority figure changes that; only the servo rate, the deflection the reversal needs (the game's rudders sit at ~40 %, the bench's pin — `rcTc` in your next log is the number), or the margin. 2.6.12 removed the dip by not pre-paying and you got 10–15° reversals; that is the same trade from the other side.

## What to fly
Same checklist. Columns: `twoAxisCap` (the cap), `rcAOpp` / `rcTc` (its capacity and dead time — the two numbers to check against what the yaw then actually did in each reversal), `paceGateZ` (1 = the window latched on saturation), `unmetZ`.
