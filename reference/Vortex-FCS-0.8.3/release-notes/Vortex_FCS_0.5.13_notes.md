# Vortex FCS 0.5.13: the hover yaws about its own pivot (no bank for the yaw)

19 Nov 2026. On 0.5.12.

**Status: benched (13.8 t, plus one 11.9 t and one 7 m/s wind case), not flown.**

Tom, 19 Nov:

> the f-35 demo, it has basically zero visible banking for yaw at least when stationary

> but yaw on the f-35b irl is also very snappy and it does a 90deg in about 6-7s

> could keeping the coupling make it more robust along side with the new logic? if so go a build 0.5.13

> also when i command positive vertical speed and then a short moment later i command yaw, yaw took a while to kick in. It doesnt happen when i command both at the same time. Check if there is any other similar problem to that and fix

## The idea

- The hover's yaw is the nozzle pushing the tail sideways. That push turns the jet and moves its centre of gravity (CG), but one point of the jet does not move: the point k ahead of the CG, k = yaw inertia / (mass x nozzle arm) = 3.2 m at 13.8 t (the centre of percussion, like the sweet spot of a bat).
- So a nozzle yaw turns the jet about that point by itself. The CG swings round it at k x yaw rate (1.3 m/s at full pedal), and the same push takes that speed away again when the yaw stops.
- 0.1.0-0.5.10 held the CG still, saw that swing as skid and banked against it after the fact. 0.5.12 banked ahead of it and made the yaw wait for the bank.
- **0.5.13 holds the pivot still instead** (`StovlYawPivot`, dev cfg, `[9. STOVL]`, on). The side-speed loop and the position hold look at the pivot (CG side speed + k x measured yaw rate; CG position + k along the nose). Nothing is banked for the yaw, and the yaw waits for nothing in the hover.
- No gains. k is the one 0.5.12 already computes every tick from the nozzle's geometry and thrust.
- Fore-aft needed no change: the pull that keeps the CG on its circle (k x yaw rate squared, 0.57 m/s2 at full rate) is already what the turn term of 0.4.7 asks for.

## What is kept of 0.5.12's coupling

- **Dropped in the hover:** the bank that cancels the nozzle's push. It would fight the swing the pivot needs.
- **Kept at speed:** in a pedal turn the bank still has to carry speed x turn rate, so the yaw rate still rises and falls only as that bank arrives and leaves (the same model of the bank: tilt rate, governor, bank loop). It fades in with speed (distance flown in one bank lag against the pivot arm: full from about 15 kt at 13.8 t).
- **Feedback for model error:** if k is off, what is left shows as side speed at the pivot and the side loop takes it out as before.
- The pedal pirouette also holds the pivot now.

## Bench (13.8 t, stick released; times from the pedal's start, which ramps over 0.1 s)

**Stationary (hover):**

| | 0.5.10 | 0.5.12 | 0.5.13 |
|---|---|---|---|
| Full pedal 6 s: largest bank | 6.3 deg | 5.4 deg | **0.3 deg** |
| Full pedal: time to 90 % of yaw rate | 1.30 s | 2.03 s | **1.28 s** |
| Full pedal: yaw stopped after release | 1.40 s | 2.13 s | **1.42 s** |
| Full pedal: side speed at the pivot | 1.55 m/s | 1.34 m/s | **0.06 m/s** |
| Full pedal: pivot's farthest drift / at the end | 6.40 / 6.12 m | 6.07 / 6.02 m | **0.26 / 0.08 m** |
| Full pedal: CG's farthest drift | 0.78 m | 0.26 m | 5.87 m (it circles the pivot) |
| Half pedal: largest bank | 4.1 deg | 4.2 deg | **0.1 deg** |
| Half pedal: time to 90 % | 0.72 s | 1.43 s | **0.72 s** |
| 0.6 s tap: largest bank | 4.3 deg | 4.9 deg | **0.1 deg** |
| 0.6 s tap: heading turned / yaw stopped | 9.0 deg / 0.97 s | 9.3 deg / 1.92 s | 8.9 deg / 0.97 s |
| Right 4 s then left 4 s: largest bank | 7.4 deg | 5.8 deg | **0.4 deg** |
| Right then left: pivot's farthest drift | 5.14 m | 4.63 m | **0.26 m** |
| 11.9 t, full pedal: bank / pivot side speed | 6.4 deg / 1.42 m/s | not run | **0.3 deg / 0.06 m/s** |
| 7 m/s wind, full pedal: bank / pivot side speed | 9.7 deg / 1.59 m/s | not run | **0.3 deg / 0.08 m/s** |

Height stayed within 1 cm in the 0.5.13 hover cases. Pitch stayed at 3.0 deg.

**Pedal turn at speed (full pedal 8 s):**

| | 30 kt: 0.5.10 | 0.5.12 | 0.5.13 | 45 kt: 0.5.10 | 0.5.12 | 0.5.13 |
|---|---|---|---|---|---|---|
| Side speed at the pivot | 0.81 m/s | 0.76 | **0.09** | 0.57 | 0.45 | **0.09** |
| Side speed at the CG | 1.25 m/s | 0.04 | 0.78 | 0.82 | 0.08 | 0.47 |
| Time to 90 % of yaw rate | 1.95 s | 2.72 s | 2.52 s | 1.57 s | 1.95 s | 1.87 s |
| Yaw stopped after release | 2.15 s | 2.22 s | 2.17 s | 1.60 s | 1.60 s | 1.55 s |
| Bank the other way after release | 3.6 deg | 0.4 deg | 0.0 deg | 3.1 deg | 1.3 deg | 0.6 deg |
| Height | -0.03 m | -0.03 | -0.03 | -0.03 | -0.03 | -0.04 |

At speed the CG's side speed is the swing round the pivot (k x yaw rate), not a skid of the flight path.

## Yaw after a climb command (your report)

Bench: full climb on the stick, full pedal 0.5 s later. Time from the pedal to half the full yaw rate:

| | 0.5.10 | 0.5.12 | 0.5.13 |
|---|---|---|---|
| Pedal alone | 0.82 s | 1.40 s | 0.80 s |
| Climb 0.5 s before the pedal | 0.70 s | **4.08 s** | 0.70 s |
| Climb and pedal together | 0.78 s | **4.45 s** | 0.78 s |
| Climb 2 s before the pedal | 0.75 s | 2.63 s | 0.75 s |

- **Cause:** 0.5.12 made the yaw wait for its bank, and the height governor holds every bank back while a climb is using the thrust. This is the case the 0.5.12 notes listed as untested.
- **Fixed in 0.5.13** for the hover: the yaw no longer waits for a bank there.
- On the bench 0.5.12 was slow with both together as well, which differs from what you saw.

## The same family, found and not fixed in this version

All three are older than 0.5.12 (0.5.10 does the same). The cause is the same in each: a climb command takes the thrust first, and the translation budget and the governor give the tilt nothing until the climb lets go of it.

| 0.5.13 bench, 13.8 t | Alone | Climb 0.5 s before |
|---|---|---|
| Side stick: time to 1 m/s sideways | 1.38 s | 4.22 s |
| Side stick: bank within the first second | 7.0 deg | 0.2 deg |
| Lever forward: time to 2 m/s | 0.93 s | 3.45 s |
| Pedal at 30 kt: time to half the yaw rate | 1.32 s | 4.48 s |

- At 30 kt with the climb the turn also skids 2.65 m/s at the pivot (0.5.10: 3.46).
- Fixing these means letting a climb share the thrust with a translation asked for at the same time, so the climb gets slower while the tilt comes in. That changes the height priority, so it is left as a decision for Tom.
- Checked and fine: a descent before the pedal (0.80 to 1.03 s to half rate, from the lower thrust), side stick before the pedal (0.83 s), pedal before the side stick, pedal before the lever (0.93 to 0.97 s).

## Checks

- **`StovlYawPivot` false:** byte-identical to 0.5.12 on 11 cases (hover full / half / tap / reversal, 30 kt and 45 kt turns, side step, lever run, crossed controls, pirouette at 30 kt, no input).
- **Both `StovlYawPivot` and `StovlYawSideFF` false:** byte-identical to 0.5.10 on the hover full pedal and the 30 kt turn.
- **`StovlYawPivot` true, `StovlYawSideFF` false:** the hover is the same (bank 0.3 deg); at 30 kt the pivot side speed is 0.49 m/s instead of 0.09, so the at-speed coupling is worth keeping.
- **Things that should not change (0.5.13 against 0.5.12):** no input and a lever run 0 -> 30 kt -> 0 within 0.013 deg of bank and 0.006 m/s; a full side step within 0.2 deg of bank and 0.33 deg of heading.
- **Not tested:** lopsided loads, Stock thrust, ship-relative mode, the pirouette beyond one 30 kt case. Not flown.
- **Build:** offline smoke test passes; the source zip rebuilds the DLL byte for byte.
- **Bench script:** `bench/scripts/vortex/piv.py` (pivot and CG side speed and drift).

## What to expect in the game

- The nose stays nearly in place and the tail swings wide: the jet turns about a point about 3 m ahead of the CG.
- The position hold keeps that point over the spot, so the CG moves on a 3.2 m circle during a pedal turn.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.13 and `meta.json`. 0.5.12 kept as `Vortex_FS20_FCS.dll.0.5.12.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.13-release`.
- No config migration. Restart the game to load it.
