# Vortex FCS 0.5.17: manoeuvring margin when heavy, caution box placement, MIN airspeed on the throttle gauges

20 Nov 2026. On 0.5.16.

**Status: benched, not flown. The caution box's new placement and the MIN label have never been seen on a screen.**

Tom, 20 Nov:

> towards the end of the log i can barely maneuver

> no, 80knt is an example. I want the min speed to leave useful maneuvering authority

> also the message overlaps the hud

> also the cockpit hud, can you add min airspeed to the throttle scale side

## What was wrong with 0.5.16's floor

0.5.16 set the "too heavy to hover" line and the speed floor on the hover **just holding its height**, with a 2 % climb reserve. At the floor, every bit of thrust goes into holding the height, so nothing is left to tilt. Every STOVL manoeuvre is the lift being tilted: side stick, the lever's starts and stops, and the pedal's coordinated turn at speed. So with nothing left to tilt, you can barely manoeuvre. I had said the law's 1.00 line kept a manoeuvring margin. It doesn't: it keeps only the 5 % control reserve for the yaw nozzle and the roll posts, not translation authority.

## 1. `StovlManeuverTilt` (dev file, 20°; 0 = 0.5.16)

The margin is now stated as a tilt: how far the lift can be tilted while it still holds the height.

- **Too heavy to hover** when the hover can't give that tilt: hover T/W × cos(tilt) < 1.00. It clears above 1.02.
- **Speed floor:** the lowest airspeed at which the wing takes enough weight off the thrust for that tilt:

  lack = g·(1.02 − T/W·cos tilt) + λ_V·h_int, and V = √(2·m·lack / (ρ·S_wing))

  Here S_wing is the wing's lift area at the hover attitude: the measured zero-AoA area plus the lift slope × attitude.
- **20° = 0.36 g** of sideways or fore-aft acceleration, before the thrust-lag reserve.

Bench, Stock 13.68 t, at the floor:

| Tilt | Floor | Translation budget | Pedal turn | Side stick |
|---|---|---|---|---|
| 0° (0.5.16) | 30.6 kt | 1.26 m/s² | 3.9°/s | 1.05 m/s |
| 15° | 44 kt | 2.29 m/s² | 4.8°/s | 1.78 m/s |
| **20° (default)** | **50.1 kt** | **2.80 m/s²** | **5.1°/s** | **2.89 m/s, 12° bank** |
| 25° | 57 kt | 3.28 m/s² | — | — |

The pedal's turn rate at speed is limited by budget / airspeed, so it grows slowly. At 25° the floor runs into the speeds where the CTOL laws take over, so 20° is the default.

**The trade-off, your call:** at 20° the caution comes on from **Stock 12.76 t / Buffed 14.82 t**. That means a Stock jet at 12.8 t, which 0.5.16 let hover, is now held to a rolling landing. At 15° it still hovers, with less margin (table). One number in the dev file; it takes effect at once.

Other bench results at 20°:

- **12-case identity set:** identical to 0.5.16 except S128 (Stock 12.8 t, now latched).
- **Regression set:** only h_s1288 changes. The jet now flies off to the floor; yaw 7°/s, height −0.10 / +0.06 m.
- **Rolling landings:**
  - Buffed 16.5 t: floor 59 kt, touchdown sink −1.5 m/s, no bounce.
  - Stock 13.68 t: floor 49.5 kt in the air.
- **With `StovlManeuverTilt = 0`:** the 12-case bench is byte for byte 0.5.16. None of the 12 cases reaches the floor in the air, so the term below is not exercised there.

## 2. The height loop's integrator in the floor

The floor now adds the height loop's integrator, λ_V·h_int, to the lift it has to find. The integrator is the force model's measured error: a force-model bias raises the floor instead of eating the margin.

## 3. In-game telemetry for the zero-budget question

In your 20 Nov log (Stock 13.68 t), the translation budget was **0 at 43–47 kt** and the pedal gave 0.3°/s. The bench gives about 2 m/s² there. **That gap is not explained.** The FCS log now writes the terms, so the next flight shows which one is off:

| Column | Meaning |
|---|---|
| `stTw` | hover T/W (law scale) |
| `stTwMan` | hover T/W × cos(manoeuvring tilt) |
| `stFloor` | speed floor, m/s |
| `stWing` | measured wing lift, m/s² |
| `stHInt` | height-loop integrator |
| `stALag` | thrust-lag reserve, m/s² |
| `stGLift` | lift the laws plan on, m/s² |

## 4. Caution box steps below HUD readouts

The `LOW HVR T/W: STO/RVL ONLY` box covered the game's bordered heading/bearing box under the message line. The box now steps below any small HUD readout in its column, as many times as needed for stacked readouts.

- It skips readouts taller than 4 text lines or wider than 0.4 of the screen, and readouts above the game's own message box.
- It looks for those readouts again every 3 s.

**Not seen on screen.**

## 5. MIN airspeed on the throttle gauges

While the caution is on, **MIN nn KT** appears under each throttle gauge's label: a copy of the gauge's own label, about 0.65 × its size. The STOVL detent indicator moves down a line to make room.

- **On the runway:** shown once the wing's zero-AoA lift has been measured in flight this session. Without that measurement the floor counts only the lift slope × hover attitude, which read 85.6 kt on the runway for the 49.5 kt it holds in the air (bench, Stock 13.68 t).
- **Why not from the airfoils:** the Vortex's lifting parts have symmetric airfoils at zero incidence, so they give 0 m² of zero-AoA lift. The flight measurement reads 44 m², because it counts the flaps and every other lifting part. So the readout uses the measurement or shows nothing.
- **No measurement yet:** the floor itself, in the air, uses the conservative value (higher floor) until the first sample, which comes right after lift-off.

**Not seen on screen.**

## Unchanged

- The thrust-lag reserve fix (`VhLagGov`) stays a bench-only experiment, off.
- The 0.5.16 pitch rise in the mission case (24 → 29°, from the wing term) is still there.

## Please fly

A heavy Stock jet (about 13.7 t), with the FCS log on. Fly the speed floor and try a pedal turn and a side stick there. The new columns will show whether the game's budget is still near zero, and why.
