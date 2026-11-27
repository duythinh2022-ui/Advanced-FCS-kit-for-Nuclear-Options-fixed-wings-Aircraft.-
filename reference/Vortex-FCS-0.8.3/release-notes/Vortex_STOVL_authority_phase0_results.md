# Vortex STOVL authority plan: phase 0 results (17 Nov 2026)

Phase 0 of `Vortex_STOVL_authority_TRC_plan.md`: measure only, no behaviour change. Tom, 17 Nov 16:49, chose "build phases 0-4, stop only if a derived value is more than 15 % off today's".

**Outcome: stopped after phase 0.** Most derived values are more than 15 % off today's at the reference, so nothing was built past the telemetry. Nothing is installed; the game still runs 0.3.11.

## 1. What was measured, and on what

- **Bench:** 0.3.11 source plus telemetry only. The working copy flies identically to 0.3.11: the STOVL regression set (`regr.sh`) prints the same lines, and the four phase-0 cases match to 0.000 on every shared column.
- **Reference:** clean, full fuel, 11 880 kg, sea level, stock thrust.
- **Other weights:** 20 % fuel (9 640 kg), 12 800 kg, and the buffed jet (thrust × 1.12, 11 880 kg).
- **Game:** Tom's flight log `Vortex_FCS_20261117_163703.csv` (0.3.11, 11.6-12.1 t, 9 min, many hover inputs). Used to check the bench's lag numbers against the real game.
- **Not measured:** 2 000 m, store loads, one-sided loads. 60 kt was replaced by 30 kt, because from 40-60 kt the CTOL blend takes the stick and the STOVL outer loops no longer act.

## 2. Inner-loop lag (how fast each rate loop follows its command)

"Lag" here is the time constant of the best first-order fit from the rate command to the measured rate.

| Axis | Bench, hover | Game log, hover | 1 / rate gain (no feed-forward) |
|---|---|---|---|
| Roll (τ_p) | 0.02-0.03 s (0.09 at 20 % fuel) | 0.02-0.07 s | 0.17 s |
| Pitch (τ_q) | 0.19 s | 0.20-0.23 s | 0.20 s |
| Yaw (τ_r) | 0.12 s | 0.08-0.14 s (one segment 0.02) | 0.40 s |

- Bench and game agree to about 0.05 s.
- Roll and yaw follow much faster than 1/K because of the feed-forward (the commanded acceleration is handed to the allocator directly). Pitch has no feed-forward in the STOVL laws, so it sits at 1/K.
- At 30 kt a 13.5° bank step fits 0.22 s on roll, but that is the roll posts saturating (29.5 deg/s asked from ~23 deg/s² of authority), not lag. Yaw is 0.10 s there. Pitch was not excited enough to fit.
- The attitude loops' own closed-loop lag is 1 / their gain, as expected: bank 0.39-0.40 s (bench and game), pitch 0.50 s (bench).

## 3. Group A: every derived gain is off today's by more than 15 %

The plan's damping rule is k = 1/(2τ). Today's gain × τ should then be 0.5. It is not, on any axis:

| # | Loop | Today | Today × τ | Rule gives | Change |
|---|---|---|---|---|---|
| A1/A2 | Bank | 2.5 /s | 0.13 | 10 /s (τ_p 0.05 s) | × 4 |
| A3 | Pitch attitude | 2.0 /s | 0.42 | 2.4 /s (τ_q 0.21 s) | + 19 % |
| A4 | Heading hold | 2.5 /s | 0.30 | 4.2 /s (τ_r 0.12 s) | + 67 % |
| A4 | Heading-hold rate cap | 20 deg/s | | 20.5 deg/s (`YawRateMax` at the reference) | + 3 % |
| A5 | Side profile's bank lag | 0.32 s (key) | | 0.10 s (1 / 10) | − 69 % |
| A6 | Fore-aft profile's pitch lag | 0.50 s | | 0.42 s | − 17 % |
| A7 | Position hold, side | 0.3 /s | | 0.63 /s on today's bank loop, 2.5 /s on the rule's | × 2 to × 8 |
| A7 | Position hold, fore-aft | 0.3 /s | | 0.5-0.6 /s | × 1.7 to × 2 |
| A8 | Centre-detent speed hold | 0.3 /s | | 1.95 /s (thrust, τ_V 0.256 s) | × 6.5 |
| A9 | Sideslip loop | 1.5 /s | 0.18 | 4.2 /s | × 2.8 |
| A10 | Commanded-climb gain (`kVRateL`) | 1.06 /s | | 1.95 /s | + 85 % |
| A11 | Auto-airbrake integral | 0.5 | | 11 at 30 m/s, 43 at 15 m/s | × 22 to × 86 |

What this means:

- **Today's outer loops are slower than the damping rule allows, on every axis.** The rule would tighten all of them, roll by far the most.
- **Lag is not what limits these loops. Authority is.** With lags this small, the `Stop()` profile (the √(2·a·e) part, which is already authority-based) sets the big moves. The linear gain only shapes the last few degrees: at 2.5 /s and 22 deg/s² the hand-over is at 7° of error; at 10 /s it is at 0.4°.
- **A5 collides with Tom's 20 % cut.** The key says 0.32 s, but the bank loop really lags 0.39-0.40 s (measured, bench and game). Deriving the lag from today's gain would put it back to 0.40 s. For 0.32 s to be true, the bank gain has to be 3.1 /s.
- **A10 was mis-described in the plan.** The 0.35 is not a leftover: 0.2.8 kept it on purpose as the slow gain for big thrust swings. Bench at 11 880 kg, full-stick climb then release: settled under 0.1 m/s in 2.0 s (was 3.5 s), no reversal, same RPM range. Only this weight was run, so the 0.2.8 bounce (−0.63 / +0.63 m/s) is not ruled out at others.
- **A11 tunes something that is nearly off.** See C4 below.

## 4. Group B: translation speed and tilt rate

Reference hover: stopping acceleration `aHlevel` 4.22 m/s², spare lift 1.09 m/s², τ_V 0.256 s, roll authority 22.9 deg/s² (after the 75 % share and the load factor), pitch 33.3, yaw 16.4.

**Calibration at the reference:**
- `StovlTrcStopTime` = 7.72 / 4.22 = **1.83 s** (the plan guessed 1.6).
- `StovlTiltStopTime` = 10 / 22.9 = **0.436 s** (set on roll).

**What that gives elsewhere:**

| Jet | Full-stick side speed today | Derived | Side tilt rate (authority) | Pitch tilt rate (authority) | Thrust term at full tilt |
|---|---|---|---|---|---|
| Reference, 11 880 kg | 14.9 kt | 15.0 kt | 10.0 deg/s | 14.5 deg/s | 10.6 deg/s |
| 20 % fuel, 9 640 kg | 14.9 kt | 32.6 kt | 9.1 | 13.8 | 16.0 |
| Buffed, 11 880 kg | 14.9 kt | 25.9 kt | 10.0 | 14.8 | 14.5 |
| 12 800 kg | 14.8 kt | 9.1 kt | 10.0 | 14.5 | 7.8 |

- **The speed moves a lot with spare lift.** Tom's own jet looks like the buffed one (his log shows 18.0 deg/s² of yaw authority at 12.1 t against the stock bench's 16.4 at 11.9 t). Calibrated on the stock reference, his full side stick becomes about 26 kt. Calibrated on the buffed jet instead (1.06 s), the buffed jet keeps 15 kt and the stock jet with full fuel gets 8.7 kt.
- **One stop time for both tilt axes makes the fore-aft tilt 45 % faster at the reference** (14.5 against 10 deg/s), because pitch has 45 % more authority than roll. Over 15 %.
- **The thrust term** (reserve = 20 % of the spare lift, the governor's share) binds only near full tilt: 10.6 deg/s at the reference, 7.8 at 12.8 t.
- For comparison, the game log: full stick held 7.58-7.75 m/s (15 kt), bank 18-19°, stop in 3.75 s after release.

## 5. Group C: physics fades

- **C1 (wing share) works as planned.** Wing-lift share at the reference: 0.044 at 15 m/s, 0.406 at 45 m/s. Run on √share (airspeed over the speed where the wing alone carries the jet, 0.21 → 0.64), the reference curve is identical to today's, not just its ends. Off-reference the band moves with √(mass / density): 13.5-40.5 m/s at 20 % fuel, 15.6-46.7 at 12.8 t.
- **C2 / C3 (slip share) cannot be built as planned.** The sideslip stiffness comes from the controller's slip probe, and the probe only runs above 30 m/s true airspeed (and only after its sign latches above 60 m/s). The two fades run at 10-25 m/s, where the stiffness is zero or stale: 0.000 through the whole short take-off, a frozen 0.073 at the end of the decel. `wPed` has the same blind spot today; it happens to read "hover" there, which is right.
- **C4 (airbrake share) is not worth building.** The panel's drag constant is 1.0 (game asset), so fully open it gives 0.09 m/s² at 30 m/s and 0.02 at 15 m/s. And since 0.2.9 the auto airbrake is off under the CTOL blend, so in the decel after a conversion it stays shut until about 58 kt (bench decel: 0 % above 60 kt, 38 % at 50 kt).
- **C5 (short take-off wheel load):** see question 6.

## 6. Group D: thrust-fit weights

The fixed 1 : 0.25 weights mean the fore-aft tolerance is 2 × the vertical one. Measured, taking the vertical tolerance as the spare lift and the fore-aft one as the acceleration the pitch tilt can still give:

| Jet | Ratio | Weights vertical : fore-aft |
|---|---|---|
| Today (fixed) | 2.0 | 4 : 1 |
| 20 % fuel | 2.3 | 5 : 1 |
| Buffed | 2.6 | 7 : 1 |
| Reference | 3.9 | 15 : 1 |
| 12 800 kg | 5.4 | 30 : 1 |

The direction is physical (less spare lift, more vertical priority), but the reference moves from 4 : 1 to 15 : 1. Over 15 %.

## 7. Seen in the 16:37 log (0.3.11, first flight)

The vertical take-off right after the conversion stayed in place: stick aft from 28 s during the conversion, nozzle at 0.94 when STOVL mode came in, ground speed 0.14 m/s at most through the lift-off. That is the 0.3.11 fix working in game.

## 8. Decisions needed before building

1. **Group A.** Tighten the loops to the rule (table in section 3), or keep today's gains and only make the chain consistent, or drop the group?
2. **Tom's 0.32 s.** If today's gains stay: raise the bank gain to 3.1 /s so 0.32 s is the loop's real lag, or let the profile go back to the real 0.40 s?
3. **Group B reference.** Calibrate the 15 kt on the stock jet (buffed gets ~26 kt) or on the buffed jet (stock gets ~9 kt)? And one tilt stop time for both axes (fore-aft tilt 45 % faster) or keep the fore-aft tilt at today's 10 deg/s at the reference?
4. **C2 / C3.** Leave the two fades on airspeed, or scale the last probed stiffness with dynamic pressure so it exists below 30 m/s (new logic in the STOVL file only)?
5. **Group D.** Build with the reference at 15 : 1, or leave the fixed 4 : 1?
6. **C5.** "Let the FCS absorb the friction": read as "don't keep weight on the wheels for the tyres' sake". The fore-aft loop already cancels rolling friction without knowing the friction coefficient, because its force request is built on the measured acceleration. What the 30 → 90 % ramp really sets is how planted the jet is (nose-wheel steering; on the wheels the laws ask for zero roll rate and do not fly a bank) and how far down the nozzle points during the roll. Carrying everything from a standstill would need the attitude loops on during the ground roll. Is that what is meant?

Recommended, in the same order: tighten A but with a floor on the lag (roll 0.07 s = the feed-forward's 0.05 s filter plus one tick, so bank 7 /s) and a Dev switch; (2) falls away; calibrate B on the buffed jet and keep one stop time; leave C2 / C3 on airspeed; build D behind its switch; drop A11 and C4; build A10 and C1; leave C5.

## 9. Where the work is

- `C:\Games\NO mods\Vortex-FCS-authority-wip\`: the working source (0.3.11 + telemetry, `VhLagL` bench switch for A10), this file, and the phase-0 scripts in `bench/scripts/vortex/` (`p0.sh`, `p0an.py`, `p0trend.py`).
- Not built for the game, not installed.
