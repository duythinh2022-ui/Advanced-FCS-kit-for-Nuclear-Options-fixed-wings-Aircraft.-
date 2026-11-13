# F-22E FCS 2.8.1 — capacity less the allocator's unmet; shed pace from the measured shed; rate cap on the estimator

Base: 2.8.0. **Compiled + bench-checked, not flown.** 2.8.0 kept as `.dll.2.8.0.bak`. (`TradeMeasured = false`, `SweepShedCos = 0.5`,
`SweepShedK = 0.6` is 2.7.6's pitch side; the estimator change below is in both paths' telemetry.)

## From the 15:22 / 15:29 flights (2.8.0)
- **Pitch hitch:** gone in both logs (no re-pull with `qCmd` held under the ask).
- **Rolling at 22–34° AoA, 220–340 kt:** |pS| median 70–73 deg/s against 2.7.6's 59; sideslip p90 5–6° against 3°, max 9.7 /
  14.2 (both in reversals at ~28° / 300 kt with the rudders at the stops: 70° of travel to cross, the window holding the
  target while they do). 2.7.6's 59 was the trade's accidental pre-cut; the 41 % rate cap alone gives 70.
- **Released-roll push-overs from 65° at 195–220 kt** (r 63–70 deg/s at the push): 16–20°; held-roll 7–10°; from ~45 deg/s
  of yaw: 10–14°. The shed cap did pace (`yawSatPitch` −40…−52 through 65 → 40°), but the capacity it paced to read 2.3× what
  the yaw actually shed (the allocator reporting 55–90 deg/s² of yaw unmet while the probe still read 30–40 available), so
  the sweep ran 65 → 17° in 1.5 s while the rudders crossed −35 → +30 and the leaf's 50 deg/s of yaw put 14° on; the last 17°
  then took 0.5 s with 26 deg/s of yaw left: 20.5° (15:29, 78.7 s).

## Changes
1. **`CapacityEstimator`:** the probe's share of the capacity is reduced by the allocator's own unmet toward that side —
   a probe cannot promise what the allocator is already failing to deliver of the demand it has. Offline on the 16 push-overs
   of the three flights: estimate / realised shed at AoA > 30° 2.34× → 1.31× (AoA < 30°: 2.63 → 1.77, part demand-limited).
2. **Shed cap** (push-over): paces to the yaw acceleration the axis is *measurably* shedding (`ceWdZ` against the rotation),
   floored at `SweepShedMeasFloor` 0.4 of the capacity and never above it; `SweepShedK` 0.6 → 1 (the rotation is shed before
   the AoA reaches zero). The sweep now waits while the rudders cross and follows the shed as it comes.
3. **Rate cap** (the 41 % blend's reversal model): its capacity toward the reversal side is the estimator's figure plus the
   passive stiffness at the margin, instead of the table headroom (1.8× in the 12:31 flight).

## Bench (sideslip ~2× high)
Regression set vs 2.8.0: within ±0.6° on 13 of 18; worse: roll-then-pull-rev 400 10.4 (2.8.0 8.7, 2.7.6 5.3 — the pinned-rudder
reversal; the bench's rudders pin where the game's sit at 40 %), pull-then-pushover+roll 450 7.5 (6.4 / 3.8); better:
push+roll 200 8.0 (8.8), push+roll 300 3.0 (3.6). The 490 km/h hitch manoeuvre: `qCmd` 14 → 60 monotonic. The bench cannot
reproduce the game's 28° / 300 kt rolling regime (its rolls sit at 42 deg/s there) nor the leaf's yaw rate at the push.

## The 28° roll rate (Tom: "I want the same right before this")
By the 15:22 log's arithmetic the steady command is raw − blend × (raw − cap): raw 109, blend 0.41, command 70 → the cap reads
~14 deg/s there (the model holds that no reversal from that state fits the margin). 2.7.6's 59 at the same point needs
`RollPaceRateBlend` ≈ 0.53 — your knob, your call; it raises the tax everywhere by the same ratio. The inherent fix is the cap
not reading 14 where the game reverses from 50 (its model is a far-stop crossing at the tuned brake), which is the next item.
