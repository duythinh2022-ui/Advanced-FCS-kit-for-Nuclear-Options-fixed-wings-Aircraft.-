# Vortex FCS 0.6.0 (20 Nov 2026)

0.6.0 replaces 0.5.26, released earlier the same day: Tom asked to fold it into 0.6.0. It has two STOVL changes. Part 2 is 0.5.26's change, unchanged.

## Part 1: Stability Assist off at speed, a pull while banked no longer yaws the jet into a departure (`StovlFreeWingAxes`, dev, on)

### What Tom asked
- "i departed at the end of the log, why wasnt it rolling around the velocity vectpr"
- "so i accitdentally turn off stability assist?"
- "then override 0.5.26 into 0.6.0"

### What the 12:13 log shows (Vortex_FCS_20261120_121354.csv, 11.59 t, Buffed)
| t (s) | What happened |
|---|---|
| 291.7-293.5 | Full left stick at 38-49 m/s, Stability Assist on, CTOL blend. Roll rate -48 deg/s, sideslip under 1.5 deg, so the roll was about the velocity vector. The bank stopped at -54 deg. |
| 293.59 | Stability Assist off. This is the game's flag: the FCS only reads it. The LogOutput line is "Stability Assist off in STOVL: throttle set to 85.2 %". The stick was centred, 0.1 s after the roll input ended. The log also has an off/on pair at 216.11 / 216.53 s while stationary. |
| 293.9-296.5 | Full aft stick with the Stability-Assist-off ("freestyle") law. The law commands an Euler pitch rate, 30 deg/s against the horizon. At -55 deg of bank that becomes q = 30 cos(bank) = 17 deg/s and r = -30 sin(bank) = +24.6 deg/s, a yaw to the right in a left bank. A coordinated turn there needed r of -6 to -9 deg/s. Sideslip went -1 -> -16 -> -31 -> -46 -> -63 deg in 2.5 s, and the jet departed. |

The freestyle law (0.2.2) maps the stick and the released heading hold to Euler rates, which is right in the hover. Only the roll was faded to a velocity-vector roll with airspeed (the wing-borne share wVv); the pitch and the heading hold were not.

### The change
The same wing-borne share wVv now also takes the pitch and the yaw from Euler rates to the airframe's own axes:
- **Stick held:** the stick is a body pitch rate.
- **Stick released:** the attitude hold keeps its pitch part, thetaDot cos(bank). That brings the attitude back at any bank, since the Euler pitch rate from it is thetaDot cos²(bank), which is never negative.
- **Yaw:** the yaw rate keeps the sideslip still. At zero sideslip with no side force, beta' = p sin(AoA) - r cos(AoA) + g cos(theta) sin(bank) / V = 0. The velocity-vector roll already supplies the p sin(AoA) part, so the yaw command is r = g cos(theta) sin(bank) / (V cos(AoA)).
- **Released pedal:** it holds zero sideslip instead of the heading, with the heading hold's own stop: Stop(beta, 2.5, aYaw, 20). Without it, a full-stick roll the yaw could not follow left 10 deg of sideslip, which the zero-rate rule alone keeps.
- **Pedal held:** it stays a body yaw rate on top.
- **Hover (wVv 0):** unchanged.

### Bench (Buffed 11.59 t, full lever from the hover, full left stick for 1.8 s, Stability Assist off at ~47 m/s and -28 deg of bank)
| Case | 0.5.26 | 0.6.0 |
|---|---|---|
| Full aft stick 3 s: largest sideslip during the pull | 46.2 deg | 0.4 deg |
| Half aft stick 3.6 s: largest sideslip during the pull | 36.2 deg | 0.1 deg |
| ...then full right stick for 1 s: largest sideslip, sideslip at 42 s | 44.9 deg, 44.9 deg | 8.0 deg, -0.1 deg |
| Wings level, full aft stick 3 s | identical | identical |
| Hover, Stability Assist off (roll, small pitch, full pedal) | identical (0.000) | identical (0.000) |

The bench case did not reproduce Tom's -55 deg bank (the bench roll stopped at -28 deg), so the log's own numbers are the case for the larger bank.

### Not fixed
Holding full aft until the nose is vertical (about 3 s at 30 deg/s) and then letting go still tumbles, in both 0.5.26 and 0.6.0. The jet is stalled at 60-75 deg AoA. The released stick's bank hold works on Euler angles, which flip at 90 deg of pitch (bank -33 -> +91 in one step), and the hold then asks for 30 deg/s of roll, which at that AoA is mostly body yaw. The tumbles differ in detail: one bench case (F1) bottomed out lower in 0.6.0 (2 m vs 32 m above the ground), another (F3) higher (39 vs 20 m).

## Part 2: lever back to the aft detent from speed pitches once (was 0.5.26)

### What Tom asked for
- "when i command accel and then right back to deccel, the jet tends to wiggle for deccel"
- "no on top fix"

### What the log shows (Vortex_FCS_20261120_113649.csv, 12.06 t, Buffed)
The lever went to full (172.5 s), the jet reached 48 m/s, and the lever came back to the aft detent (0.20) at 179-181 s. The pitch then did this:

| Phase | t (s) | Pitch | Why |
|---|---|---|---|
| CTOL blend | 181-186 | 7 -> 10.4 deg | CTOL pitch law walks the AoA up to 10 deg (aRefW) |
| Blend ends (wC -> 0) | 188-190.5 | 10.4 -> 7.5 deg, q -1.5 deg/s | STOVL wing attitude, Lerp(hover 3 deg, gamma + alphaRef, wing share), is lower; no fore-aft tilt yet |
| Hover hand-over (lamV, lamT -> 1) | 190.5-195 | 7.5 -> 37 deg, q up to 11 deg/s | fore-aft tilt allowed only now; decel demand steps 4.0 -> 7.7 m/s^2 |
| Stop ends | 195-199.6 | 37 -> 3 deg | tilt comes out |

Between the blend's end and the hand-over the jet was still mostly on the thrust (wing ~25 % at 37 m/s), but it braked only with the nozzle and the airbrake: ~1 m/s^2 achieved for the 4 asked.

### The two causes, and what changed
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

### Bench (Buffed unless noted; lever full, then back to the aft detent)
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

### Not verified
- Not flown.
- The bench's closed-loop replay of the 11:36 log window did not fly the STOVL state (it starts at -18 deg pitch), so it is not used. The scripted A48 case reproduces the log's shape: 10.3 / 7.6 / 36.4 / 3.0 vs the game's 10.4 / 7.5 / 37.1 / 3.0.


## Bench sets for 0.6.0 as a whole
- 12-case identity set: identical to 0.5.26.
- Regression set: unchanged from 0.5.26.
- With `StovlFreeWingAxes` false, 0.6.0 is 0.5.26.
- With `StovlTiltAllRegimes` and `StovlLeverStopModel` false as well, it is 0.5.25.
- Benched, not flown.
