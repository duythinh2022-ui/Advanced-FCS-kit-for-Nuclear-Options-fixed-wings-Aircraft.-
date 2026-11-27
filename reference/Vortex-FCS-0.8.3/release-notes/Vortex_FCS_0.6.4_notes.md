# Vortex FCS 0.6.4 (20 Nov 2026)

On 0.6.3: the same attested native core (sha256 `e20c7b99...feaa9b2`, embedded byte for byte). STOVL law change only: the released v/s stick's stop.

## What Tom asked
1. "Why descent is worse???" (0.6.3's descent stop)
2. "Is the spool time has spool rate and spool acceleration accounted in? Also would using spool acceleration model for prediction beneficial"

## Why 0.6.3's descent was worse
0.6.3 asked a closed-form deceleration, a = j (sqrt(tau^2 + 2 v / j) - tau), and held it to 80 % of the cap. On a descent the cap is the spare lift, so the stop never used the last 20 %. Bench, 12 t, full-stick descent: settle time 2.62 s in 0.6.2, 3.60 s in 0.6.3.

0.6.3 also had a problem nobody had reported yet. In CS5 (a climb released with a bank held) the yaw rate reached 5.8 deg/s and the heading swung 3.2 deg; 0.6.2 had 2.35 deg/s and 0.8 deg. The closed form's single response time (tau) was too short for that case. A longer tau stopped the swing but made every stop slow.

## The engine has no spool acceleration
The game's engine law is `rpm += clamp(target - rpm, ±spoolRate) × dt`:
- far from the target, the RPM moves at the flat spool rate;
- close to it, the RPM closes as a 1 s lag;
- nothing ramps the rate itself up, so there is no spool acceleration to model.

What looks like spool acceleration is where the law switches from the flat rate to the lag, plus the FCS's own over-drive of the target (`RpmLead`). A closed form can only approximate that switch. A predictor that runs the law itself does not need to.

## Change (`StovlEngineResponse`, dev, on; false = 0.6.2)
**The predictor.** `PredictStopEnd` runs the game's engine law forward from the RPM the engine has now, in 0.02 s steps for up to 4 s. Each step:
- the FCS over-drives the target as `ThrottleForThrust` does, within idle and the reachable RPM (the damage state's ceiling, 0.6.1);
- the lift follows the RPM: thrust ratio (rpm - 0.85 idle) / span;
- everything else that pushes the jet vertically is held at what it is now. That is the measured vertical acceleration less the lift's, with both taken through the same 0.1 s low-pass so the measurement's lag does not read as a push.

The result is the speed still left when the lift is back at the weight.

**The stop.** Every frame the stop asks for the deceleration aC that leaves no speed. The predictor assumes aC is held for one RPM lead (the time the throttle's over-drive takes to bring the RPM onto its target) and the thrust is then sent back to the hover's.
- If even the full cap leaves speed, the stop asks for the full cap: 4 m/s^2 for a climb, the spare lift for a descent. There is no 80 % hold any more.
- If the hover's thrust alone already leaves none, the stop asks for nothing.
- In between, aC is the root of the predicted end speed, found by regula falsi in up to 6 predictor runs.

There is no deceleration profile, no assumed response time, and no tuning gain. The 0.6.3 closed form is gone.

**Tried and not kept:**
- A proportional taper on the predicted end speed. It left a slow tail where the brake stayed partly on.
- Holding the push term in proportion to the speed (as drag would be). It overshot (CS5: -0.47 m/s) and chattered.

## Bench
Buffed, stick released after 2 s. Each cell is the speed past zero after the stop (m/s) / the time until |vs| stays under 0.05 m/s (s).

| Case | 0.6.2 | 0.6.3 | 0.6.4 |
|---|---|---|---|
| 12 t, take-off climb 0.6 stick (3.6 m/s) | 0.00 / 2.48 | 0.04 / 1.33 | 0.00 / 1.52 |
| 12 t, full-stick climb (6 m/s) | 0.23 / 3.95 | 0.04 / 2.32 | 0.00 / 2.40 |
| 12 t, full-stick descent (5 m/s) | 0.07 / 2.62 | 0.08 / 3.60 | 0.00 / 2.43 |
| 12 t, half-stick climb | 0.00 / 2.53 | 0.04 / 1.32 | 0.00 / 1.52 |
| 15 t, take-off climb 0.6 stick | 0.00 / 2.77 | 0.01 / 1.90 | 0.00 / 1.65 |
| 15 t, full-stick climb | 0.00 / 1.98 | 0.01 / 1.75 | 0.00 / 1.47 |
| 15 t, full-stick descent | 0.03 / 2.98 | 0.04 / 3.43 | 0.00 / 3.05 |
| 15 t, half-stick climb | 0.00 / 2.25 | 0.01 / 1.65 | 0.00 / 1.38 |
| CS5 (6 m/s climb released with a bank): worst sink, peak yaw rate, heading swing | -0.24, 2.35 deg/s, 0.8 deg | -0.03, 5.82, 3.2 | -0.04, 2.19, 0.75 |

Nose swing during the stops is within 0.05 deg of 0.6.2 everywhere.

**Curve shapes.** The deceleration holds the cap and then comes off along the engine's own curve in one smooth step, with no dip or bounce. At the end a tail of about 0.02-0.06 m/s remains for a few tenths of a second: the predictor is slightly pessimistic about the last part of the spool-back, and the brake comes back on lightly.

**Worse than 0.6.2:**
- 15 t full-stick descent: 0.07 s longer. It no longer runs 0.03 m/s past zero.
- Regression case vl_b15 (15 t, 4 m/s take-off climb, StovlSinkRate 12): settle 1.75 -> 2.13 s. Speed past zero went from 0.037 to 0.002 m/s. The time is lost in the 0.05-0.15 m/s tail.

## Sets
- **12-case set:** changes only where a stick climb or sink is released.
  - CS5's worst sink: -0.24 -> -0.04 m/s.
  - Thrust-request variation is lower in 9 of 12 cases (CS5 160 -> 149, L 216 -> 181) and within 3 % in the other three (K45, PB, S0).
  - S128's worst sink: -0.14 -> -0.15 m/s.
  - The take-off climb's stop ends 0.3 m lower, so later hover heights are 0.3 m lower.
- **Regression set:**
  - Touchdowns within 0.03 m/s.
  - Hover pedal/crab within 0.01.
  - VTO, STO and mission unchanged.
  - The landing cases' 6 m/s climb stop settles 0.6-1.0 s sooner than 0.6.2 (b1188 4.37 -> 3.38 s, b135 4.10 -> 3.43 s, s1188 3.42 -> 2.83 s).
- **Deceleration set:** pitch shape, peak rates and time to stop unchanged. The hover height is 0.3 m lower (the take-off stop).
- **Low-T/W airborne (Stock 14.5 / 15.5 t):** unchanged.
- **Stability-Assist-off set:** F2 and H1 are close to 0.6.2. F1 and F3 end differently, because both run into the known tumble at vertical (Euler-angle bank hold, see 0.6.0) from a start 0.3 m lower. The 0.6.2 law started 0.36 m lower gives the same F3 result (sideslip 85 deg, 46 km/h at the end).

## Bench tooling
`stopA` and `stopV` columns (the stop's deceleration and predicted end speed) were added to the bench csv.

## Not verified
- Not flown.
- The predictor's lift-per-RPM comes from the FCS's thrust model. A model error is corrected only as an offset, through the measured push.
