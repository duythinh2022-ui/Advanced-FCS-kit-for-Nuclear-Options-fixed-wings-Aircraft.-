# F-22E FCS 2.3.3: pitch-rate capture plans for the effectors' unwind

Base: 2.3.2. One addition, `RateCaptureEffectorLag` (Dev "2. Envelope", default 1). New telemetry column `rateCapLag` (the unwind time used, s).

## The event (21 Oct 23:44 log, 255 s)
Release from a 35 deg/s pull at 60 deg AoA, 118 m/s. The capture commanded the nozzles to -20 (full nose-down) to brake the rate; the rate crossed zero at 0.3 s with the nozzles still at -19 and they took another 0.35 s to come back through zero at 70 deg/s: q ran to -10.6 deg/s, 1.1 s below -2.

## What the dip looks like otherwise (that log, 8 releases, mean / median)
Median q over the negative stretch -1.1 / -0.95 deg/s, stretch 1.3 / 1.5 s, time below -2: 0.36 / 0.28 s, worst instant -3.7 / -2.9. 2.0.9 same flight style: -0.7 / -0.8, 0.7 s, 0.07 / 0.00 s, -1.5 / -1.3. The 255 s event is a different mechanism from that small dip and is what this build addresses.

## Change
The rate trajectory's deceleration used to come off over a fixed 0.15 s (a3 / j) as the command was reached, whatever the effectors were doing. Now, while braking a pitch rate toward the command (stick released; not on onset, not in AoA protection or an unload), the end jerk is a3 / t_unwind, with t_unwind = RateCaptureEffectorLag x (the nozzles' |command| / slew or the stabs' |command| / servo, whichever is longer) + 0.06 s. The deceleration is off by the time the slowest pitch effector can be back at trim, so the rate arrives at zero with the effectors near trim instead of still at their braking stop.

## Bench
- Pull-release, no roll, 300-600 km/h, holds 0.35 / 0.8 / 1.3 s: undershoot 300 km/h / 1.3 s -1.2 -> -0.1 deg/s, 500 / 1.3 s -1.9 -> -0.9; the rest within 0.1. Zero crossing within 0.1 s of before.
- **The bench does not reproduce the -10.6 overshoot** (its nozzles reach -17 and the capture is clean), so this is built from the mechanism in the log, not from a bench reproduction. The in-game log will say.
- Suite: S2 unchanged; S1 (pull-release-roll) mean -2.2 -> -2.4, worst -4.3 -> -7.8 in one case (450 km/h, 0.2 s hold, roll 0.15 s after release). That case is byte-identical through 1.5 s and only diverges at the roll stop, where one stab parks at -30 and the nozzles go to +20 in one run and not the other: the same stuck-set knife edge as the release cases, triggered by a 0.01 deg difference, not by the capture. Reported as is.

## Not verified
- Not flown.
