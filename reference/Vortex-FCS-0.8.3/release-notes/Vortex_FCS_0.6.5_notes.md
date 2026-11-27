# Vortex FCS 0.6.5 (20 Nov 2026)

On 0.6.4: the same attested native core (sha256 `e20c7b99...feaa9b2`, embedded byte for byte). STOVL law change only: the released v/s stick's stop predictor.

## What Tom asked
1. "But would it be better to make the fcs more robust and it absorbs the drag"
2. "Do it and improve the worse cases, no on top fixes"

The worse cases were the two where 0.6.4 settled later than 0.6.2 by the 0.05 m/s measure:
- the 15 t full-stick descent;
- the regression set's vl_b15 (a 4 m/s climb at 15 t).

## What the push really was
0.6.4's predictor took one number for everything the lift model does not explain: the measured vertical acceleration less the RPM's lift. It then held that number constant for the rest of the stop.

Before modelling drag, I logged that number inside the FCS and fitted it on the bench runs:
- **Drag is small.** The fitted term is -0.002 to -0.009 m/s^2 per (m/s)^2. That is 0.07-0.3 m/s^2 at 6 m/s, and almost nothing near the end of a stop.
- **The lift per RPM is high by 1-2.5 %.**
- **The largest error was a measurement error.** The FCS's acceleration input is the root body's velocity derivative, already through its own 0.05 s filter, so it trailed the RPM's lift by about 0.04-0.05 s. On top of that it carries the pitch acceleration times the CG's lever arm. While the RPM moved, 0.6.4 read that lag as a push.

Measured from the derivative of the CG's own vertical speed (the speed the stop flies), the delay term drops to about 0.

## Changes (`StovlPushEstimator`, dev, on; false = 0.6.4, bit-identical)
1. **Measurement.** The push is now measured from the CG vertical speed's derivative. It goes through the same single 0.1 s low-pass as the lift model.
2. **In-flight estimator.** Recursive least squares fits four terms:

   push = c0 + c1 × lift + c2 × d(lift)/dt + c3 × v|v|

   - c1: the lift per RPM off by a share.
   - c2: the force trailing the RPM (a delay).
   - c3: drag.

   The fit uses the hover's own data. It forgets over 10 s, but only while the data is moving; otherwise directions nothing excites would blow up. Its covariance is never allowed above the prior. The fit itself runs unbounded. Physical bounds apply only where the prediction uses the terms: c1 within ±30 %, c2 within 0..0.2 s, c3 against the speed.
3. **Prediction.** The predictor runs the game's engine law with the learned slope, delay and drag. The outside push (what is left after them) is measured live.
4. **Hold.** The brake is held for the RPM lead plus the learned delay.
5. **Margin.** The stop aims to leave the speed the fit has been wrong by: the fit's measured error (as a standard deviation) times the time the thrust still needs to come back. It shrinks to nothing as the thrust gets back. Early in a flight the measured error is larger, so the margin is larger.

**Tried and not kept** (all model-based, all benched):
- **Drag only, as the speed share of the whole push.** It scaled the model error too. Result: overshoot (CS5 -0.47 m/s) and thrust chatter.
- **Shorter brake holds (0.017, 0.05, 0.067, 0.10 s).** These were faster, and they did beat 0.6.2 on both worse cases at the 0.05 band (15 t descent 2.97 s, vl_b15 1.80-1.88 s). But the end of the stop went unstable: a 2-frame throttle wiggle at the end of CF5. The RPM's first frames carry most of a demand's effect, so each new plan counted it again. Only a hold as long as the RPM lead was stable.
- **Time-optimal full-cap brake with a solved switch time.** A brake committed frame by frame did the same, as did a brake held until the RPM reached its target (that one also had two roots). All of them chattered while the spool-back pinned the throttle. There, every deceleration below the one that unpins the throttle gives the same engine, so the end speed does not move with the brake.
- **Asking that unpinning deceleration** instead. It removed the chatter but over-braked. My model's unpinning point differs from the FCS's own throttle mapping (thrust direction, allocation), which caused overshoot of 0.06 m/s at 15 t.

## Bench (Buffed; stick released after 2 s)
Each cell is the time until |vs| stays under 0.05 / under 0.02 m/s (s). No case runs past zero by more than 0.003 m/s in 0.6.4 or 0.6.5.

| Case | 0.6.2 | 0.6.4 | 0.6.5 |
|---|---|---|---|
| 12 t, take-off climb 0.6 stick | 2.48 / 3.30 | 1.52 / 1.85 | 1.40 / 1.72 |
| 12 t, full-stick climb | 3.95 / 5.30 | 2.40 / 2.72 | 2.27 / 2.53 |
| 12 t, full-stick descent | 2.62 / 3.85 | 2.43 / 2.82 | 2.32 / 2.55 |
| 12 t, half-stick climb | 2.53 / 3.33 | 1.52 / 1.88 | 1.40 / 1.70 |
| 15 t, take-off climb 0.6 stick | 2.77 / 3.67 | 1.65 / 1.93 | 1.60 / 1.88 |
| 15 t, full-stick climb | 1.98 / 2.57 | 1.47 / 1.85 | 1.55 / 1.82 |
| 15 t, full-stick descent | 2.98 / 3.60 | 3.05 / 3.30 | 3.05 / 3.23 |
| 15 t, half-stick climb | 2.25 / 2.87 | 1.38 / 1.75 | 1.47 / 1.73 |
| vl_b15 (15 t, 4 m/s climb) | 1.75 / 3.13 | 2.13 / 2.40 | 2.07 / 2.32 |
| CS5 worst sink | -0.24 | -0.04 | -0.03 |

**The worse cases.** At 0.05 m/s, 0.6.2 still settles first in the 15 t descent (2.98 vs 3.05 s) and in vl_b15 (1.75 vs 2.07 s). That is because 0.6.2 runs past zero by 0.03-0.04 m/s, which counts as settled under 0.05, and then takes another 0.6-1.4 s to really settle. At 0.02 m/s, 0.6.5 is the fastest of the three in every case:
- 15 t descent: 3.60 -> 3.23 s;
- vl_b15: 3.13 -> 2.32 s.

**Against 0.6.4:**
- Better or equal at 0.02 in every case.
- At 0.05 the 15 t full- and half-stick climbs are 0.08-0.09 s later, because the margin leaves a little more for the end.

**Curve shapes.** The same as 0.6.4: the brake holds the cap, then comes off smoothly. At the very end of K30C's stop there is a ±0.02 throttle flicker for about 0.9 s. 0.6.4 has a shorter one in the same place.

## Sets
- **12-case set:** throttle reversals over 0.05 within two of 0.6.4 in every case.
  - Thrust-request variation within 12 % of 0.6.4 everywhere (K30C 170 -> 187, as 0.6.2's 185; HY 26 -> 29).
  - Small (>0.01) throttle flips: K30C 14 -> 29, the end-of-stop flicker above; within ±5 of 0.6.4 in the other cases.
  - Worst sinks within 0.02 m/s.
- **Regression set:**
  - Touchdowns within 0.02 m/s.
  - Hover pedal/crab within 0.01.
  - VTO, STO and mission unchanged.
  - The landing cases' 6 m/s climb stop settles 0.1 s sooner than 0.6.4 (0.6-1.1 s sooner than 0.6.2).
- **Deceleration set:** pitch shape and peak rates unchanged; time to stop within 0.4 s of 0.6.2.
- **Low-T/W airborne (Stock 14.5 / 15.5 t):** unchanged.
- **Stability Assist off:** F2 and H1 are close to 0.6.2. F1 and F3 run into the known tumble at vertical (Euler bank hold), as in 0.6.4.

## Bench tooling
New bench csv columns:
- `aLiftF`, `pushB`: the lift model and the measured push;
- `pushC1`, `pushC2`, `pushC3`: the learned terms;
- `pushSd`: the fit's error.

## Not verified
- Not flown.
- The estimator starts empty each flight, so the first stop after take-off uses whatever the take-off climb has taught it.
- In game, the CG speed's derivative may be noisier than on the bench.
