# FS-20 Vortex FCS 0.3.11 (17 Nov 2026)

On 0.3.10. Tom, 16:28: "When the nozzle is still moving into position … i commanded vertical speed … the jet moved forward. Fix that, it is suppose to stay in one place. Also set and migrate default StovlYawStopTime to 1.25s".

## 1. Vertical take-off before the nozzle is down: the jet now stays in place

**Log 16:20, 13–24 s:**
- STOVL conversion on the runway, lever in the aft detent (0.20). Doors 0 → 1 over 13–17 s.
- During the conversion the nozzle stayed aft (swivel 0.00).
- Stick aft from 14 s. At 17.0 s STOVL mode came in with the nozzle at 0.05 and its target at 0.66–0.95, and the nozzle takes ~4 s to swing down.
- The thrust came up at once (thrOut 0.76–0.88 by 18–19 s) along a nozzle still pointing mostly aft.
- The jet rolled forward to 7.9 m/s (gs) before lifting off, and the hover hold then had to pitch it up to 17° to stop.

**Causes:**
- **(a)** The conversion kept the nozzle aft even on the wheels at a standstill.
- **(b)** The thrust fit weighs the vertical force over the fore-aft one, so with the nozzle aft it still asked for lift-off thrust.
- **(c)** The wheel hold released as soon as the stick asked for a lift (`HoldWheels` needed no lift intent).

**Fixes:**
1. **Nozzle pre-positioned.** On the wheels below 5 m/s, the nozzle goes to its ground position (`StovlGroundSwivel` 0.95) while the doors open. Both take ~4 s, so it is down when STOVL mode comes in.
2. **VTO thrust held to the nozzle.** On the wheels, with lift intent and the lever in or behind the aft detent (zero ground speed asked), the thrust is capped so its forward component along the nozzle as it is now gives at most 0.5 m/s² (`VtoMaxRollAccel`, internal). The jet lifts when the nozzle points down. This is a backstop for when the nozzle isn't down yet.
3. **Wheel hold through the VTO.** The wheel hold stays on through a vertical take-off from the wheels (lever in the aft detent) until the wheels leave the ground. A short take-off (lever forward) is unaffected.

**Bench**, Tom's sequence: conversion on the runway, lever 0.20, stick full aft from 1.7 s (during the conversion), held.

| | Nozzle when STOVL comes in | Lift-off | Ground run before lift-off | Peak ground speed |
|---|---|---|---|---|
| 0.3.10 | 0.00 (aft) | 8.5 s | 13.2 m | 9.7 kt |
| **0.3.11** | **0.95 (down)** | **6.6 s** | **0.9 m** | **2.0 kt** |
| 0.3.11, nozzle forced to start aft (`StovlGroundSwivel` 0), backstop only | 0.00 | 9.7 s | 1.4 m | 2.0 kt |
| same, backstop off | 0.00 | 7.8 s | 6.6 m | 9.3 kt |

## 2. `StovlYawStopTime` 1.09 → 1.25 s (Tom's value)

- Moved once from either earlier default (1.09 or 1.84) by `VortexConfigRevision` 8. A hand-set value is kept.
- Full pedal = the yaw authority × 1.25 s: ~19 deg/s at 20 % fuel (15.3 deg/s²), ~21 deg/s with full fuel. A 90° turn takes ~6 s.

## Regression (STOVL set vs 0.3.10; CTOL unchanged, STOVL-only change)

- **Decel:** identical.
- **VL:** within 0.2 m and 0.3°.
- **Hover:** the VTO ground run is 0.46 m (was 0.68). The pedal turns are a little faster (1.25 s).
- **The STO case** (`vto`: lever full forward at 6.3 s, 1.8 s after STOVL mode) **changes**. The nozzle is now already down when the lever goes up. In 0.3.10 it was still at 0.38 on its way down, the same transient as the log.
  - From the full-down nozzle the roll starts slower: 4.4 vs 16.9 kt at 8 s.
  - It lifts off a little later and climbs out at 16° pitch, not 25°, with more speed (98 vs 84 kt at 24 s).
  - That is the take-off a pilot gets whenever STOVL mode has been on for a few seconds on the runway, now also right after a conversion.

Benched, not flown.
