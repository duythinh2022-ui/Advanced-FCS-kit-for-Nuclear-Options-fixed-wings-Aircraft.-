# F-22E FCS 4.8.2: low-speed AoA limit, TVC-off pitch rate, roll-stop bounce, effector twitching

Base: 4.8.1 as installed. All changes come from Tom's 22:35 flight (`F22E_FCS_20261125_223538.csv`; two-zone on, refit 0.25, TVC off 0-200 s). Not flown.

## 1. Low-speed AoA limit with TVC off (`LowThrustCapMarginScalesWithQ`, dev 2. Envelope, default true)
- **Log, 140-180 s:**
  - Below about 160 km/h the swept authority cap sat on its 20° floor.
  - The swept nose-down margin at the jet's own AoA was still +7 to +23 deg/s^2.
  - The rule wanted a fixed 8 deg/s^2 at every AoA, and nose-down scales with dynamic pressure, so every AoA failed at low speed.
  - When the speed came back, the cap crept up at 1°/s with the jet sitting on it: 24° to 38° at full aft over 164-178 s.
- **Change:**
  - The required margin scales with dynamic pressure: full from 300 km/h, down to 0.15 of it.
  - The cap rises at 4°/s whenever its target is more than 5° above it. The 1°/s rise now applies only near the target.
  - The approach to the limit is still held to what the nose-down can stop, so the pitch rate slows instead.
- **Bench:** the bench never drops the cap, in either version or airframe, so this change is untested there. With the new rule, a 180/230 km/h idle pull and push recovers 55° to 30° in 2.2/2.6 s, the same as 4.8.1.

## 2. TVC-off pitch rate (`AoARateLimitFullNoseDown`, dev 2. Envelope, default true)
- **Log, 420 km/h full aft, 33-39° AoA:**
  - The pitch command sat at the flight-path rate + 6 deg/s (= `AuthRateMin`): 25 deg/s, with AoA rising at 6-8 deg/s.
  - The pilot's command was 45-50 deg/s.
  - The cause: the AoA-rate limit (active while the low-thrust cap is in force, which is always the case with TVC off) judged the stop on stabs + slats only. The cap itself counts the whole nose-down (flaperons, rudder toe, slats), and its margin there was +50 to +63 deg/s^2.
- **Change:** the AoA-rate limit uses the whole nose-down.
  - From the logged margin, the limit there becomes about 55 deg/s, so the command becomes the pilot's 45-50: about 1.8x in that band.
  - Below 33° and from 40° the AoA-rate limit did not bind. From about 47° the AoA capture (braking floor 4 deg/s^2) and the IAS pitch-rate budget (60 down to 32 deg/s) still limit, unchanged.
- **Bench:** not reproduced. The bench sweep credits more stab nose-down, so the limit never bound there.
- **Capture braking kept on stabs and slats.** On the whole nose-down, X2e at idle overshot the 45° cap by 2.5-4.6° on the bench.

## 3. Roll-stop bounce (`TwoZoneStabPeakClamp` margin -3; 4.8.1 used +3)
- **Log, 216.7 and 254.2 s:** full right roll at 10° AoA, about 450 km/h, TVC on.
  - The stop from 160-170 deg/s bounced to -37/-45 deg/s.
  - After the roll had stopped, the right stab stayed commanded to -26...-30 (trailing-edge down) and the nozzles pitched up +16-19 against it. At low AoA that stab was in the reverse-roll region (outboard zone past its peak), so the allocator kept using it for roll the wrong way.
- **Change:** below 20° AoA the outboard zone is now held 3° short of its lift peak (4.8.1 allowed 3° past it).
- **Bench:**
  - Replay of 253.6-256.3 s: bounce -26 to -13 deg/s (the game showed -45).
  - Reverse-pose case: 11/0/9 rows to 0 at three speeds.
  - Low-AoA roll and push within 2 %.

## 4. Twitchy effectors (`StabBranchHold`, dev 4. Gains, default 0.3 s)
- **Log, chatter across the whole flight:**
  - Every surface and the nozzles flipped direction by 5° or more within 0.6 s, 46-82 times a minute. The slats did not (0-2 a minute).
  - The worst windows were at 170-215 km/h and 22-36° AoA.
  - At 373.3 s the right stab command alternated +35 / -6...-15 and the nozzles -20 / -5 on every tick.
  - That tick-by-tick flipping tracked `stabBranch` toggling 0/1: one tick the stall-avoiding branch logic pinned the stab, the next tick the free allocation took it back past the peak. 65-70 % of all tick-to-tick command flips coincided with a branch toggle.
- **Change:** a stab put on its branch stays pinned there for 0.3 s after the free solve last went deep.
- **Bench replay, 368-386 s:**
  - Stab command swings 97 to 53-73 per minute.
  - Nozzle tick-flips 43 to 17 per minute.
  - Worst sideslip 5.0° to 7.0° in this replay.
  - The bench reproduces only part of the game's flipping (64-77 against 217-343 tick-flips a minute in that window).
- **With `StabBranchMaxAoA = 30`** instead of 42: swings 47 a minute, nozzle tick-flips 0, sideslip 3.2°, but roll tracking error 3.8 to 5.5 deg/s. Not the default.
- **Not addressed:** the slower (0.1-0.6 s) hesitations that are not branch flips.

## Log columns added
`authMargReq` (cap margin required), `authRateUp` (AoA-rate stop limit), `capBoundU` (capture bound), `qMax` (final pitch-rate upper bound).

## Battery, 450 km/h, TVC off/on (4.8.2 against 4.8.1, single runs)
Roll, push, pull, stop and overshoot are within run-to-run noise on both airframes, except one effect: X2e reversal at 50° is 28.0 deg/s (was 26.9).

## Not verified
- Not flown.
- Changes 1 and 2 target game behaviour the bench does not reproduce. The estimates come from logged values.
