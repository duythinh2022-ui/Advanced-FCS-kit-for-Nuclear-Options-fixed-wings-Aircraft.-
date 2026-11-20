# Vortex FCS 0.5.12: yaw without the side skid (yaw and bank on one force balance)

19 Nov 2026. On 0.5.11 (the Detents frame-drop fix, which is included unchanged).

**Status: benched on one jet (your 13.8 t), not flown.**

Tom, 19 Nov:

> so yaw creates side skidding at the start and stop, could the roll post be coupled so that the side skidding is gone or very small

> not like only at start and stop yaw, but also whenever the fcs modulates the yaw rate

> also maybe do the same thing for side skidding, evertime the command changes, yaw must also be corrected as the same time not reactionary ... so basically 1 logic but works both ways

> the least fixed gains as possible, more model and physisc base to be as bullet proof as possible

## Where the skid came from

- In the hover the yaw is made by the main nozzle swinging sideways on its bearing. That pushes the tail sideways, and the push moves the whole jet as well as turning it.
- The size is fixed by the airframe: side acceleration = k × yaw acceleration, with k = yaw inertia / (mass × the nozzle's distance behind the centre of gravity). On your jet k = 3.2 m (3.3 m measured from 0.5.10's bench trace).
- The roll posts are vertical, so they can't push sideways. What they can do is bank the jet, which tilts the lift. Full yaw acceleration needs about 5° of bank to cancel.
- 0.5.10 only banked after the side speed had appeared, so the bank arrived about 0.9 s late at every yaw start and stop.

## What 0.5.12 does (one balance, both axes)

1. **The bank comes in with the yaw.** The sideways demand now includes the bank that cancels the nozzle's push for the yaw acceleration the pedal still needs.
2. **The yaw waits for the bank.** The pedal's yaw rate changes only as fast as that bank has had time to build. The FCS follows the yaw's share of the bank through the same limits the real bank has: the tilt rate, the height governor and the bank loop.
3. **Same balance at speed.** In a pedal turn at 30–45 kt the bank also has to carry the turn itself (speed × turn rate). The yaw rate now rises only as the bank is there for both.
4. **Covers every pedal-rate change**, not only the start and stop: the turn limit moving, the authority changing with fuel and thrust.

- **No gains of its own.** k comes from the nozzle's geometry and thrust each tick. The timing uses the bank's existing tilt rate and lag margin (`StovlTrcBankRate`, `StovlTiltStopTime`, `StovlTrcLagMargin`).
- **Left alone:** the pedal pirouette (raw yaw, as before) and the heading hold's own small corrections.
- **Side stick → yaw needed no change.** The allocator already cancels the roll posts' cross-moments in the same tick. Bench, full side stick in the hover: the heading stays within 0.10°, the same in 0.5.10 and 0.5.12.
- **Switch:** `StovlYawSideFF` (dev cfg, `[9. STOVL]`, default true). False gives 0.5.10's behaviour exactly.

## Bench (13.8 t, stick released)

**Stationary (hover):**

| | 0.5.10 | 0.5.12 |
|---|---|---|
| Full pedal 6 s: skid at start / stop | 0.64 / 0.65 m/s | **0.04 / 0.09** |
| Full pedal: farthest drift from the spot | 0.78 m | **0.26 m** |
| Full pedal: time to 90 % of yaw rate | 1.20 s | 1.93 s |
| Full pedal: yaw stopped after release | 1.30 s | 2.03 s |
| Full pedal: largest bank | 6.3° | 5.4° |
| Half pedal: skid at start / stop | 0.48 / 0.49 m/s | **0.04 / 0.03** |
| Half pedal: time to 90 % | 0.62 s | 1.33 s |
| 0.6 s tap: skid | 0.58 m/s | 0.19 |
| 0.6 s tap: heading turned | 9.0° | 9.3° |
| Full right then full left: skid at reversal | 0.82 m/s | **0.08** |
| Full right then full left: farthest drift | 1.35 m | **0.24 m** |

**Pedal turn at speed (full pedal 8 s):**

| | 30 kt, 0.5.10 | 30 kt, 0.5.12 | 45 kt, 0.5.10 | 45 kt, 0.5.12 |
|---|---|---|---|---|
| Skid in the turn | 1.25 m/s | **0.04** | 0.82 | **0.07** |
| Skid after release | 0.46 m/s | **0.03** | 0.33 | 0.08 |
| Bank the other way after release | 3.6° | 0.4° | 3.1° | 1.3° |
| Time to 90 % of yaw rate | 1.85 s | 2.62 s | 1.47 s | 1.85 s |
| Yaw stopped after release | 2.05 s | 2.12 s | 1.50 s | 1.50 s |
| Heading turned in 8 s | 89° | 88° | 60° | 59° |

Height stayed within 3 cm in every case.

## The cost

- **The yaw is slower to start and stop in the hover**: about 0.7 s more to reach full rate and to stop. That is the time the roll posts need to bring the bank in and take it out again (about 10°/s on this jet). The full yaw rate is unchanged.
- **A short tap turns the nose the same amount**, but later and more smoothly: the yaw keeps going for about a second after you let go.
- **Each yaw start and stop dips a wing about 5°**, into the turn at the start and the other way at the stop. That bank is what cancels the skid.

## Checks

- **Switch off:** byte-identical to 0.5.10 on four cases (hover pedal, 30 kt and 45 kt pedal turns, side step).
- **Switch on, things that must not change:** byte-identical to 0.5.10 on the pirouette at 30 kt (4 s and 8 s), both Stability Assist off cases, a lever run 0 → 30 kt → 0, and a full side step.
- **Not tested:** other weights, lopsided loads, a jet with no thrust margin (the governor would hold the bank back, so the yaw would be slower still), ship-relative mode. Not flown.
- **Build:** offline smoke test passes; the source zip rebuilds the DLL byte for byte.
- **Bench script:** `yss.py`.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.12 and `meta.json`. 0.5.11 kept as `Vortex_FS20_FCS.dll.0.5.11.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.12-release`.
- No config migration. Restart the game to load it.
