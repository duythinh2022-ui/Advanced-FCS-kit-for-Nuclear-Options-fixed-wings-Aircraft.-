# Vortex FCS 0.6.3 (20 Nov 2026)

On 0.6.2: the same attested native core (sha256 `e20c7b99...feaa9b2`, embedded byte for byte). STOVL law change only.

## What Tom asked
1. "check the v/s cmd, idk if it is placebo, but before, when i release the v/s command the jet stop like solidly ... But now move up, the stop has a very small sink with the jet sways forth/back very slightly, same for down"
2. "is the spool lag plan on everything for stovl, if not do it if it is beneficial"
3. "Do it and model in actual engine response speed for everything without the fixed assumption"

## The v/s stop: what it was
The stop had not changed since at least 18 Nov:
- In the game logs, climb-stop sink was 0.07-0.26 m/s with 0.4-0.5 deg of nose swing in every log from 18 Nov to 0.6.2.
- On the bench, 0.5.21 through 0.6.1 gave the same result within 0.002.

The mechanism (bench, 12 t, full-stick climb):
- The stop was a distance profile, min(k e, sqrt(2 a e)), from a stopping point set at the release.
- It asked its whole deceleration at once. The thrust cannot drop that fast, so the jet was still at +5.18 m/s 0.2 s after the release.
- The loop saturated at -4 m/s^2 to catch up. The throttle went to idle and then about 0.3.
- At the end the demand went from -4 to +0.5 m/s^2 in 0.5 s, faster than the engine could spool back. Result: -0.23 m/s, then +0.14 m/s.
- The nose dips about 0.3 deg from the thrust cut.

## The engine law
From the game, and the bench plant's copy of it:

`rpm += clamp(target - rpm, ±spoolRate) × dt`

- The RPM closes on its target as a 1 s lag, never faster than the spool rate.
- The FCS over-drives the target (`RpmLead`). With the target at the throttle's end, the RPM can rise at min(spoolRate, reachable RPM - rpm) per second and fall at min(spoolRate, rpm - idle).
- The reachable RPM is the damage state's ceiling (0.6.1).
- `EngineLag` (1 s) is that law, not a tuning value. It is now documented as such. `EngineRpmRate(rpm, up)` is the whole law.

## Changes (`StovlEngineResponse`, dev, on; false = 0.6.2)

1. **The released stick's stop.** It is now a deceleration asked from the speed left:

   a = j (sqrt(tau^2 + 2 v / j) - tau), held within holdA

   - **j:** the lift rate (m/s^2 per second) at which the engine law moves the RPM from the hover RPM: up after a climb's stop, down after a descent's. It equals that RPM rate × the vertical lift per RPM / mass.
   - **tau:** the RPM lead, the delay before the RPM starts moving.
   - **holdA:** the stop's 80 %: 3.2 m/s^2 for a climb, 80 % of the spare lift for a descent.

   Effects:
   - The deceleration comes off exactly as fast as the thrust can come back.
   - There is no stopping point to chase. When the speed reaches zero the height hold takes the height there.
   - The thrust the jet has at the release counts by itself: a stick eased off has cut it, the deceleration asked is smaller than the jet's, and the thrust starts back at once.
2. **The tilt governor's way up** is also held to the spool rate. The way down already was.
3. **The roll-post / fan dip caps** (`rUp`, `rUpS`) are held to the spool rate's lift rate.
4. **The post and fan trims** (the post and fan use the thrust has already made up) move at the vertical axis's measured response time, `tauV` (the RPM lead plus the spool time). Before, they used the game's 1 s lag, which applies only to a target pinned at the throttle's end. The effect is neutral on the bench (within 0.01 m/s).

**Tried and not kept:** replacing the 1 s with the measured response (`tauV`, ~0.25 s at 12 t) in the tilt governor and the dip caps. That lets tilts in faster than the thrust follows:
- 12.8 t Stock roll case: worst sink -0.13 -> -0.68 m/s.
- 12 t pitch-and-bank case (PB): worst sink -0.08 -> -0.29 m/s.

There the 1 s is the right figure, because a tilt pins the throttle at its end.

**Also tried and not kept:** a distance profile with a jerk-limited end. It was clean on full-stick stops but bounced by up to 1 m/s on eased-off and half-stick stops.

## Bench (Buffed; stick released after 2 s; worst opposite-way speed after the stop, time to |vs| < 0.1)
| Case | 0.6.2 | 0.6.3 |
|---|---|---|
| 12 t, full-stick climb (5.0 m/s) | 0.23 m/s, 1.85 s, nose 2.69-3.21 deg | 0.04 m/s, 2.27 s, nose 2.70-3.14 |
| 12 t, full-stick descent (4.9 m/s) | 0.07 m/s, 2.05 s | 0.08 m/s, 2.23 s |
| 12 t, half-stick climb (2.9 m/s) | 0.00, 2.08 s | 0.04 m/s, 1.27 s |
| 15 t, full-stick climb (2.1 m/s) | 0.00, 1.47 s | 0.01, 1.65 s |
| 15 t, full-stick descent (2.8 m/s) | 0.03, 2.92 s | 0.04, 3.38 s |
| 15 t, half-stick climb | 0.00, 1.53 s | 0.01, 1.57 s |
| Take-off climb (3.6 m/s), stick eased off over 0.3 s | still climbing 0.24 m/s at 2.3 s, then a long 0.01-0.05 creep | 0.10 m/s at 2.3 s, then none |

Curve shapes in 0.6.3:
- The climb stop's deceleration rises to 3.2 m/s^2, holds, and comes off smoothly to zero. No bounce.
- The 15 t descent stop is 0.46 s longer: its deceleration is now kept to the stop's 80 % of the spare lift (0.97 m/s^2), where the old loop saturated past it to 1.2.

## Sets
- **12-case set:** changes only where a stick climb or sink is released. CS5's worst sink went -0.24 -> -0.02 m/s. The take-off climb's stop ends about 0.1 m lower (15.83 vs 15.88 m in S0).
- **Regression set:**
  - Touchdowns within 0.01 m/s.
  - Hover pedal/crab within 0.01.
  - The landing cases' 6 m/s climb stop ends about 0.5 s later and 0.7 m higher, without the old 0.02-0.08 m/s creep. That moves their "hover vs before push" figure from +0.30 to +0.84 because the window starts while it is still stopping.
  - Mission and STO unchanged.
- **Deceleration set (0.5.26), low-T/W airborne stops and the Stability-Assist-off pulls:** unchanged.

## Not verified
- Not flown.
- The nose dip from the thrust change itself (about 0.3 deg) is smaller but still there. It comes from the pitching moment of the thrust change, not from the stop's timing.
