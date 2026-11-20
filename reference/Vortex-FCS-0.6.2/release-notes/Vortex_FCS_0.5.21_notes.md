# Vortex FCS 0.5.21: "can hover" line with a climb reserve, vertical take-off climb, caution text

20 Nov 2026. On 0.5.20.

**Status: benched, not flown. The new caution text has not been seen on a screen.**

Tom, 20 Nov:

> check latest log, touch downs are good. But at the beginning it shows mnvr ltd, but i cannot takeoff vertically? then i did a short takeoff, with the min speed working at least correctly from what i've seen. Also the mnvr ltd is a bit confusing, especially ltd

## Why it said you could hover but you couldn't take off

Log `Vortex_FCS_20261120_015017.csv`, 15.85 t, hover T/W **1.005** on the laws' scale (95 % of full dry thrust).

1. **0.5.20 drew the "can hover" line at 1.00.** So the caution said the jet could hover (MNVR LTD) instead of STO/RVL ONLY.
2. **At 1.005 there is almost nothing to climb on:** 0.005 × g ≈ 0.05 m/s². The laws keep the last 5 % of full dry thrust for the yaw nozzle and the roll posts.
3. **On the wheels the estimate was low.** The climb permission came from the live lift estimate. With the nozzle at its ground position (past the hover trim) that estimate reads about 4 % low. The result: full aft stick on the wheels (113–119 s) asked for no climb at all, and the thrust stayed at idle.

The short take-off and the speed floor afterwards worked as intended.

## Changes

| | 0.5.20 | 0.5.21 |
|---|---|---|
| Too heavy to hover (speed floor, **LOW HVR T/W: STO/RVL ONLY**) | Hover T/W under 1.00, cleared over 1.02 | Under **1.02**, cleared over **1.04**: the 2 % climb reserve the floor itself is sized with |
| Climb allowed on the wheels | Live lift estimate, nozzle at its ground position | Lift with the nozzle at its **hover trim**, where it will be once off the ground |
| Hovers, but with less than the 20° margin | LOW HVR T/W: MNVR LTD | **LOW HVR T/W: REDUCED MNVR** (your pick) |

Both behaviour changes are part of `StovlLowMarginHover` (dev file); false = 0.5.17–0.5.19.

## Bench (Buffed, full aft stick from the wheels)

| Mass | Hover T/W | Result |
|---|---|---|
| 15.15 t | 1.057 | Identical to 0.5.20 (VTO, hover at 9.8 m) |
| 15.5 t | 1.038 | VTO, climbs at 2.4 m/s, no speed floor (REDUCED MNVR) |
| 15.9 t | 1.016 | STO/RVL ONLY; climbs at 1.0 m/s and the floor takes it forward |
| 16.2 t | 1.001 | STO/RVL ONLY; barely lifts (0.1 m/s) |

**Other sets:** the 12-case and regression sets are unchanged, except the Stock 12.8 t vertical take-off (bank 0.02°, lowest height in the hover case 0.1 m higher).

## Release

Main DLL 0.5.21. Release folder `C:\Games\NO mods\Vortex-FCS-0.5.21-release`.
