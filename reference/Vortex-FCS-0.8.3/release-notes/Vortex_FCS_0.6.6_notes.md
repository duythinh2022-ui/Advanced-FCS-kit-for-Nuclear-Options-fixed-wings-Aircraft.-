# Vortex FCS 0.6.6 (20 Nov 2026)

On 0.6.5: the same attested native core (sha256 `e20c7b99...feaa9b2`, embedded byte for byte). STOVL law change only: the released v/s stick's stop margin.

## What Tom reported (game log 22:12, 0.6.5)
- After a sink release the jet sometimes sits at -1 to -4 ft/min.
- After a climb release it sometimes sits at +1 ft/min for a few seconds, and occasionally dips a few ft/min negative.
- It settles slower as it nears 0 ft/min, especially after a climb.
- By feel it is about 99 % flawless, with no overshoot he can see.

## What the log shows
- **Sink release at 108.7 s:** the stop brings the sink down to -0.02 m/s (-3.9 ft/min) by 112 s. It then sits there for over 5 s, with the brake off (aDesV 0) and the stop still active.
- **Climb releases at 151.7 s and 78.5 s:** the jet rests at +0.01 to +0.03 m/s (2-6 ft/min) the same way.

The stop only ends when the vertical speed crosses zero, so a speed that rests short of zero never gets finished off.

## Cause
0.6.5's margin was the fit's error times the time the thrust still needs to come back. That time was counted to the end of the predictor's run, and the run always includes the brake hold (one RPM lead, about 0.13 s). So the margin never fell below the error times 0.13 s.

In the game the fit's error is larger than on the bench, because the measured acceleration is noisier. From the log, about 0.15 m/s² or more. That puts the margin floor at about 0.02 m/s, which is exactly where the jet rested.

The same margin also counted fast noise as if it ran into the speed in full. So during the whole approach the stop aimed to leave tens of ft/min. That is the slow end Tom saw.

**Bench reproduction:** I injected acceleration noise until the fit's measured error was 0.136 m/s², about the game level. 0.6.5 then rests at +2.4 to +3.4 ft/min after climbs and -2.5 to -4.4 ft/min after descents for the whole 9 s window, never getting under 2 ft/min. That matches the log.

## Changes (`StovlPushEstimator`, dev, on)
1. **Margin time.** The time used is now when the lift is first back at the weight, not the end of the hold. The margin is zero once the thrust is back, so the stop takes the rest out and ends.
2. **Margin size.** The fit's error is treated as a Gauss-Markov process:
   - its variance, plus its correlation time, from the frame-to-frame correlation the fit measures;
   - integrated over that time: variance = 2 s² tc (T - tc (1 - e^(-T/tc))).

   A slow error (a push the fit hasn't learned) still grows with T, as in 0.6.5. Fast noise mostly averages out over the recovery.

There are no new numbers: everything comes from the fit's own measurements. With `StovlPushEstimator = false` the law is still 0.6.4, bit-identical.

## Bench
**With game-level noise (measured fit error 0.136 m/s²).** Each cell is the time until |vs| stays under 5 ft/min / under 2 ft/min (s).

| Stop | 0.6.5 | 0.6.6 |
|---|---|---|
| 12 t, climb 3.6 m/s | 2.72 / never | 2.45 / 2.75 |
| 12 t, full climb | 2.97 / never | 2.57 / 2.97 |
| 12 t, full descent | 2.93 / never | 2.67 / 2.92 |
| 15 t, climb 3.6 m/s | 2.73 / never | 2.53 / 2.77 |
| 15 t, full descent | 3.68 / never | 3.38 / 3.75 |

At 4 s after release, 0.6.5 is still at 2.4-4.4 ft/min. 0.6.6 is under 0.4 ft/min, except the 15 t descent at -1.5 ft/min.

**Without noise (as the bench has always run):** within 0.02 s of 0.6.5 everywhere. On the bench the fit's error is small, so the margin floor was too.

## Sets
- **12-case set:** within 0.6.5 everywhere: thrust-request variation within 5 %, worst sinks within 0.02 m/s.
- **Regression set:** identical to 0.6.5 (touchdowns, hover, VTO, STO, mission).

## Still there
- About 0.3 s of exponential tail at the very end. The brake comes off gradually as the RPM returns, because it is held for one RPM lead. Shorter holds went unstable at the stop's end (0.6.5 notes).
- A brief few-ft/min dip after a climb stop can still happen when the stop's last prediction is slightly optimistic. In the log, the dips at 27.7 s and 82.5 s are translation starts (stick forward), not the stop.

## Not verified
- Not flown.
- The game's actual noise structure is not in the game csv (no fit columns there). The bench noise is a stand-in.
