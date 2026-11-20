# Vortex FCS 0.5.26 (20 Nov 2026)

## What Tom asked for
- "when i command accel and then right back to deccel, the jet tends to wiggle for deccel"
- "no on top fix"

## What the log shows (Vortex_FCS_20261120_113649.csv, 12.06 t, Buffed)
The lever went to full (172.5 s), the jet reached 48 m/s, and the lever came back to the aft detent (0.20) at 179-181 s. The pitch then did this:

| Phase | t (s) | Pitch | Why |
|---|---|---|---|
| CTOL blend | 181-186 | 7 -> 10.4 deg | CTOL pitch law walks the AoA up to 10 deg (aRefW) |
| Blend ends (wC -> 0) | 188-190.5 | 10.4 -> 7.5 deg, q -1.5 deg/s | STOVL wing attitude, Lerp(hover 3 deg, gamma + alphaRef, wing share), is lower; no fore-aft tilt yet |
| Hover hand-over (lamV, lamT -> 1) | 190.5-195 | 7.5 -> 37 deg, q up to 11 deg/s | fore-aft tilt allowed only now; decel demand steps 4.0 -> 7.7 m/s^2 |
| Stop ends | 195-199.6 | 37 -> 3 deg | tilt comes out |

Between the blend's end and the hand-over the jet was still mostly on the thrust (wing ~25 % at 37 m/s), but it braked only with the nozzle and the airbrake: ~1 m/s^2 achieved for the 4 asked.

## The two causes, and what changed
Both fixes remove a gate or a fixed number. Neither adds a branch.

1. **The tilt was gated on what the controls mean, not on the physics** (`StovlTiltAllRegimes`, dev, on).
   - The fore-aft tilt brakes the jet past the nozzle's forward limit by tilting the thrust's share of the lift.
   - 0.2.7-0.5.25 computed it only in the hover regime (lamV > 0), scaled it by the lever's regime weight lamT, and multiplied it by the thrust share (1 - wWing) only after the tilt-rate limit.
   - Now:
     - it is worked out in every regime, weighted only by the thrust's share of the lift, `TiltShare` = (1 - wWing)(1 - wCtolBlend)(1 - wC);
     - the share is taken before the rate limit, so the rate-limited quantity is the tilt the attitude actually takes. That is the rate the governor and the thrust-lag reserve are planned on;
     - the references that follow the jet under the CTOL blend (`thetaRefF`, `thetaHold`) follow its attitude less the tilt, because the tilt is added after them.
   - Each part was found on the bench:
     - Ungated, with the share left after the slew: the tilt built up unflown under the wing and the blend, then came in as they faded. Result: an 8 deg/s pitch-rate bump at the blend's end, and a Stock 14.5 t low-T/W stop that went to 30 deg and 7 km/h under its floor.
     - With the share before the slew, but the references following the whole attitude: each degree of tilt went into the reference and was added again (8 deg of tilt, nose at 27 deg).
2. **The lever's aft detent asked a fixed 4 m/s^2 in the acceleration regime** (`StovlLeverStopModel`, dev, on).
   - The 0.2.8 intent was "stop as hard as the jet can". It is now exactly that: the fore-aft profile's demand for the zero-ground-speed command, inside the same translation budget the speed command uses. That is what aGs is with gsCmd 0.
   - The demand is now continuous across the lever's hand-over to the speed command. Before, it stepped 4 -> 7.7 m/s^2 at 33 -> 28 m/s.
   - `StovlDecelMax` stays only as the value used when the switch is off.

## Bench (Buffed unless noted; lever full, then back to the aft detent)
| Case | 0.5.25 pitch extremes | 0.5.26 | Peak nose-up rate | At the hover (gs < 1 kt) |
|---|---|---|---|---|
| A48, 12.06 t, from ~93 kt | 10.3 / 7.6 / 36.4 / 3.0 | 10.3 / 35.0 / 3.0 | 10.7 -> 6.3 deg/s | 52.2 -> 50.2 s |
| A60, 12.06 t, from ~117 kt | 10.7 / 7.6 / 36.4 / 3.0 | 10.7 / 35.0 / 3.0 | 10.7 -> 6.2 | 62.7 -> 61.3 |
| A35, 12.06 t, from ~66 kt | one hump, 36.5 | one hump, 35.0 | 10.8 -> 12.4 | 40.5 -> 39.4 |
| B48, 13.8 t | one hump, 28.6 | one hump, 27.6 | 8.7 -> 5.5 | 45.8 -> 44.4 |

Curve shapes in 0.5.26:
- A48 / A60: the pitch is monotonic up, then down. The rate onset has a small wobble (6.3 -> 5.0 -> 5.5 deg/s) while the CTOL blend fades.
- A35: the onset is front-loaded (11.5 deg/s for ~1 s, then 5.5-5.7, then down). In 0.5.25 the rate was a smoother bell peaking at 10.6. The pitch has no plateau.
- Height: the balloon while the nose comes up is +1.5 m (0.5.25: +1.3 m) in A48.

Low T/W, airborne at 250 km/h, Stock thrust, lever full then the aft detent:
- 14.5 t: pitch max 16.0 / 16.1 deg, lowest 111 / 111 km/h (0.5.25 / 0.5.26).
- 15.5 t: pitch max 18.4 / 18.4 deg, lowest 123 / 125 km/h, sink -0.72 / -0.31 m/s.

Regression set:
- Unchanged except the mission's stop. That went from 13 / 8 / 29 deg to a monotonic 13 -> 28 deg, with q max 8.2 -> 4.8 deg/s and the hover reached 2 s sooner.
- The 380 km/h airborne conversion decel is unchanged.

12-case identity set:
- thetaAssist is identical in all 12 cases.
- Other channels differ only by floating-point rounding: the lever's acceleration-regime value still enters `Lerp(aAcc, aGs, lamT)` at lamT = 1. The largest is 0.6 deg/s of roll rate in K45's pedal transient. Height and speed agree to ≤ 0.01.
- Both switches off: identical to 0.5.25.

## Not verified
- Not flown.
- The bench's closed-loop replay of the 11:36 log window did not fly the STOVL state (it starts at -18 deg pitch), so it is not used. The scripted A48 case reproduces the log's shape: 10.3 / 7.6 / 36.4 / 3.0 vs the game's 10.4 / 7.5 / 37.1 / 3.0.
