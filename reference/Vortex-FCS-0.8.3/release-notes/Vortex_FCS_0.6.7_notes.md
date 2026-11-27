# Vortex FCS 0.6.7 (20 Nov 2026)

Built from `Vortex_single_aero_model_design.md` on 0.6.6. The native core is unchanged (the same attested Rust core, embedded in the plugin DLL). STOVL law change only, behind the new dev switch `StovlAeroModel` (on). With it off, 0.6.7 is bit-identical to 0.6.6 (12-case set, every csv column).

## The short version
- **Built:** one learned aero model of the airframe (lift and drag from dynamic pressure and AoA, on the game's own airfoil shapes). Its users are a translation feed-forward, the turning/translation budget's wing lift, and the v/s stop estimator's push.
- **What actually fixed the excursions:** the design's 4.3 "replace `I.accel` everywhere in STOVL". The vertical loop's acceleration input was the root body's, so it carried pitch acceleration × the CG's lever arm. At the start of every stop it read +0.20 m/s² while the CG was really decelerating at −0.18, so the thrust came in late. Now the STOVL laws use the CG's own acceleration, through the same 0.05 s low-pass, so the loop timing is unchanged.
- **What the design got wrong:** the aero force is not the larger part. Measured cleanly (CG acceleration − gravity − the modelled propulsive force), the true aero change during these translations is slow: 0.27 m/s² built over 6 s. Over the thrust's response time that is 0.02-0.06 m/s² of feed-forward. The design's "0.3-0.7 m/s² unexplained push" came from `pushB`, which ignores the nozzle's direction, and from the lever-arm error above.
- **Result (bench, Tom's jet 13.8 t Buffed):** height wander during translations and stops is roughly halved to a third. Fore-aft stops hold height to ±1-4 cm instead of ±5-16 cm. The v/s peaks drop from 13-30 to 9-18 ft/min. The design's 3-8 ft/min target is not reached: what remains is a translation-start transient, explained below and not fixed here.

## 1. The model
In the plane of symmetry, with air-relative speed V along t, lift direction n, q = ½ρV², AoA a:

force / mass = q/m × [S0 cos a + kL × L(a)] n − q/m × kD × D(a) t

- **L(a), D(a):** the airframe's lift and drag shapes over −90..+90° of AoA, built at load from the game's airfoils (horizontal lifting parts' area × effectiveness × CL; drag also counts every part's parasitic term as the game's AeroJob does).
- **Parameters:** kL and kD scale those shapes (prior 1). S0 is the zero-AoA lift area the foils don't give (cambered flaps and other parts; prior 0, or the session's carried value).
- **Lift range:** lift is modelled only in the attached-flow range, between the lift table's negative and positive peaks. Outside it only drag counts. Without this, the take-off's near-vertical climb (AoA ≈ −87°) taught kL = 3.6 with S0 negative, and the first stop then predicted 2.5× the true lift (bench S128).
- **Learning:** a Kalman filter on two channels (world-vertical, and horizontal along the heading), each with a bias for propulsion-model error.
  - It forgets over 10 s (PushWindow, the push estimator's window), so it is effectively least squares over the last 10 s. That separates the bias from the q-scaled terms whenever the window holds different airspeeds, hover included.
  - The covariance is capped at the prior and kept in double precision. In single precision a 45 kt pedal turn left it indefinite and the prediction ran off to 1e7 (bench K45, first build).
- **Bounds:** they apply only to the copy the laws use, by projecting onto S0, kL, kD ≥ 0 in the filter's own metric. Clipping each parameter alone broke the S0 + kL sum the data pins down; the first build was 30 % short at 15 m/s.
- **Dropped term:** the droop-share term in the design (Sd × droop weight) is not fitted. The droop weight is ~1 throughout the hover regime and small beyond it, so the data never separated it from S0, and fitted it swung −45..+23 m², moving the speed floor with it.
- **Accuracy vs the bench's true aero force** (new truth column), hover-regime translations: 0.004-0.012 m/s² RMS, CF5 0.047. With game-level noise: 0.005. Through a long hover in noise the parameters don't move.

## 2. Where it is used

| Consumer | 0.6.7 | Effect on the bench |
|---|---|---|
| Vertical loop's acceleration (all STOVL laws except Stability-Assist-off) | CG acceleration, 0.05 s low-pass (was the root body's `I.accel`) | Almost all of the gain (ablation below) |
| Translation feed-forward | The model run at the state the commanded fore-aft acceleration and the pitch-attitude rate bring over the thrust's response time (RPM lead − learned delay + the 0.05 s measurement lag and a tick); its change goes into the vertical request. Gated by lamV. | 0 to −1 ft/min, ±0.3 cm. Small, because the aero changes slowly. |
| Turning/translation budget wing lift | The model's vertical lift at that planned state, capped as before (was `I.accel`-measured, 0.3 s low-pass) | S128 −19.3 → −16.9 ft/min; others unchanged |
| v/s stop estimator | Fits the push less the model's aero, so its lift slope and drag aren't taught the wing's lift by a translation; the predictor adds it back. At near-zero airspeed this is 0.6.6. | Stops unchanged |
| Low-T/W speed floor and MIN readout | **Not switched**, kept on 0.5.17's measurement | See 4 |

**Feed-forward attitude:** it uses the pitch-attitude rate θ̇ = q cos φ − r sin φ about the level right axis, not the body rates. Rotating the body by all rates against a held airflow read a pedal turn as sideslip. The body pitch rate alone read the turn's r sin φ (5-6°/s with the nose steady) as a pitch-up. Either way K30's height wander went from 2.9 to 4.5 cm.

## 3. Bench

### 12-case set (Tom's jet), v/s stick released windows
v/s extremes in ft/min / height wander in cm. Climb-command windows excluded.

| Case | 0.6.6 | 0.6.7 | 0.6.7 with game-level noise (0.6.6 in brackets) |
|---|---|---|---|
| F0 lever 30 kt and stop | −14.4/+15.0, −4.9/+5.3 | −9.3/+12.4, −1.6/+2.6 | −13.4/+14.4, −2.4/+2.8 (−15.0/+15.6, −5.1/+5.6) |
| PB lever + side stick | −15.0/+21.7, −4.6/+5.6 | −13.2/+12.8, −1.7/+3.1 | −10.6/+12.6, −2.4/+2.7 (−13.8/+20.7, −4.0/+5.1) |
| K30 pedal turn at 30 kt | −14.4/+13.0, −3.2/+2.9 | −13.4/+12.4, −2.6/+2.6 | −15.2/+13.0, −3.0/+2.7 (−16.1/+14.0, −3.5/+3.2) |
| K45 pedal turn at 45 kt | −17.1/+13.0, −4.0/+2.7 | −15.6/+12.2, −3.2/+2.4 | −21.1/+14.2, −4.3/+2.8 (−22.2/+16.9, −4.9/+3.6) |
| S128 Stock 12.8 t lever and stop | −29.5/+17.9, −5.3/+3.8 | −17.9/+9.6, −2.8/+2.2 | −18.1/+10.0, −1.9/+2.7 (−27.8/+14.4, −3.0/+3.7) |
| CF5 climb then lever, after the stop | −9.4/+9.8, −2.7/+2.6 | −4.5/+5.1, −0.9/+0.7 | −6.1/+5.1, −2.0/+0.1 (−12.6/+12.2, −4.8/+0.8) |
| S0 side stick | −12.0/+7.3, −1.6/+1.1 | −11.0/+8.3, −1.5/+1.3 | |
| HY hover pedal | −4.1/+5.9, −1.3/+1.0 | −4.5/+5.9, −1.2/+1.0 | |
| CS5, K30C (climb stops) | as 0.6.6 within 0.4 ft/min | | |

Noise: velocity noise σ 0.015 m/s per tick, set so the push fit's error is 0.136 m/s², the game level in the 0.6.6 notes. It is a new bench key (`BenchVelNoise`), applied identically to both versions.

### Fore-aft stops (lever back to the aft detent)

| Case | Stop time to 0.15 m/s | Height during the stop |
|---|---|---|
| Stock 12.8 t, 14 kt | 6.17 → 6.12 s | −3.2/+3.8 → −0.7/+1.0 cm |
| Buffed 11.9 t, 14 kt | 4.20 → 4.18 s | −9.4/+16.3 → −2.8/+3.9 cm |
| Stock 11.9 t, 14 kt | 5.02 → 5.02 s | −4.8/+8.2 → −1.1/+2.0 cm |
| Buffed 13.6 t, 14 kt | 4.57 → 4.57 s | −6.5/+10.1 → −1.5/+2.7 cm |
| Tom's jet, 30 kt | 7.00 → 6.95 s | −4.8/+5.7 → −1.3/+1.7 cm |
| Tom's jet, 45 kt | 8.33 → 8.28 s | −4.7/+5.0 → −1.5/+1.9 cm |

Peak thrust in the stops is 0.01-0.06 lower. Distance, peak deceleration and pitch are within 0.1-0.5° and 0.2 m.

### Ablation (each piece off on its own; F0 / PB / K30 / S128 v/s peaks)
- **CG acceleration alone:** F0 −9.1/+12.8, PB −12.6/+13.4, K30 −13.4/+12.8, S128 −16.7/+9.1.
- **Without the CG acceleration** (model, feed-forward and budget on): F0 −15.4/+19.9, PB −18.3/+22.6, S128 −28.7/+18.1. That is worse than 0.6.6: the feed-forward can't help while the loop's acceleration input is wrong.

### Other sets
- **Regression set:** touchdowns within 0.02 m/s. Gear compression on the landing cases up to 8 mm higher (0.134 → 0.142 m, break 0.5). Hover pedal/crab identical. VTO identical. Full-throttle STO and the mission end slightly differently once out of the hover (mission top 596 → 653 m).
- **Airborne deceleration from 380 km/h:** identical.
- **Stability Assist off:** H, F and G within 0.01 m/s. I and M (Stability Assist on at speed / lever pick-up) differ by up to 0.35 m/s of v/s, through the CG acceleration.
- **v/s stops:** CS5 and the VTO case's climb/sink releases as 0.6.6.

## 4. The speed floor stays on 0.5.17's measurement
Benched with the model driving the floor (Stock 14.5 / 15.5 t, airborne conversion at 250 km/h):
- The MIN speed sat 3-6 kt higher.
- It was less steady (spread 1.2-1.7 kt against 0.4-0.7).

The floor sits at 60-80 kt, in the CTOL blend, where the CTOL flap schedule and pitch trim move the lift. In steady flight the model can't tell its propulsion bias from its lift area, and it was 0.26-0.34 m/s² short there.

With the floor on the old learner, 0.6.7 gives 58.6-62.4 kt (spread 0.63) and 69.6-71.9 kt (spread 0.36), against 0.6.6's 60.3-62.9 (0.44) and 71.1-75.7 (0.69). `AeroArea()` is in the code for when the model can carry it.

**15.5 t case:** after the airborne conversion (made while climbing 2 m/s), 0.6.7 holds that 2° path with the stick released, as the path law says. 0.6.6 lost it at 23.8 s: a one-tick thrust cut to zero as the nose came down 17° → 11° (the same lever-arm error) reset its path reference.

## 5. Found, not fixed: the translation-start transient
Every lever or side-stick start still shows about −9/+13 ft/min in its first 1.5 s, at 0-5 m/s where the aero is ~0.
- **Cause:** the thrust fit chooses the nozzle swivel as if the requested thrust were already there. On a step to +30 kt the swivel leans aft at its full rate (0.886 → 0.686 in 0.8 s) while the RPM has risen 5 %. The vertical thrust drops (−0.16 m/s², −9 ft/min), then the nozzle comes back up as the thrust arrives and overshoots (+12 ft/min).
- **Model-based fix:** let the fit lean the nozzle only as far as the thrust it will have by the time the nozzle gets there carries the weight (cap the thrust in the swivel cost by the engine law over the swivel's travel time).
- **Cost:** that touches every STOVL manoeuvre and slows a translation start by roughly half a second of fore-aft acceleration. Your rule is "start and stop as hard as the jet can", with the vertical taking priority, so this is your call.

## 6. Not verified
- Not flown.
- The bench's noise is a stand-in for the game's.
- In the game the CG velocity's derivative may be noisier than the root body's `I.accel`. 0.6.5/0.6.6 already use it for the push estimator.
- The model's in-game accuracy is unknown: the game's propulsion model error goes into the filter's bias, and the bench's propulsion model is exact.

## Bench tooling
- **New csv columns:** `aeroTV`, `aeroTH` (the plant's true aero force per unit mass, vertical and along heading), `amMeasV`, `amModV`, `amFF`, `amS0`, `amSd`, `amKL`, `amKD`, `amBV`, `amSdV`, `surfW`.
- **New bench keys:** `BenchVelNoise`, plus `AmUseFF`, `AmUseCg`, `AmUseBudget` and `AmUsePush` for ablation (all on as built).
