# Vortex FCS 0.5.20: a heavy jet that can hover goes vertical again

20 Nov 2026. On 0.5.19.

**Status: benched, not flown. The new caution has not been seen on a screen.**

Tom, 20 Nov:

> The latest one was with very heavy load, on my fdr hud, i can see the twr can reach above 2 in stovl but i cant go vertical?

Asked what should happen when the jet can hover but has less than the 20° margin, Tom chose: **hover allowed, with a warning.**

## What was wrong

Log `Vortex_FCS_20261120_012539.csv`, Buffed jet, 15.15 t:

- **Hover T/W on the laws' scale:** 1.054. The jet holds its height up to acos(1/1.054) = 18.4° of tilt.
- **0.5.17's rule:** `StovlManeuverTilt` 20° counted it as too heavy to hover.
- **What that did:** the jet lifted off vertically at 3.3 m/s, then the FCS sent it forward to the 34 kt speed floor, with your lever at the zero-speed detent.

## What it does now (`StovlLowMarginHover`, dev file, on; false = 0.5.17–0.5.19)

| Hover T/W (laws' scale) | 0.5.20 |
|---|---|
| Holds height with the full 20° tilt | Normal |
| Holds height, but not with 20° of tilt (e.g. 1.00–1.064 at 20°) | Hovers and goes vertical. Caution **LOW HVR T/W: MNVR LTD**. Translation budget is what the thrust leaves. |
| Can't hold height (under 1.00; clears above 1.02) | Speed floor and **LOW HVR T/W: STO/RVL ONLY**, as before. The floor is still sized so the jet has the full 20° of margin at it. |

## Bench

**Buffed 15.15 t (your case):** vertical take-off, side stick, pedal, then push down to land.

| | 0.5.19 | 0.5.20 |
|---|---|---|
| Take-off | Sent forward to 30 kt | Straight up; hovers at 9.8 m |
| Height in the hover | — | Within 0.05 m |
| Full side stick | 2.6 m/s, at 30 kt | 2.2 m/s, in the hover |
| Full pedal | 9°/s, at 30 kt | 15.5°/s, in the hover |
| Landing | Rolling, touched at 29 kt | Vertical |

**Other cases:**

- **Stock 12.8 t** (S128 and h_s1288; hover T/W just over 1/cos 20°): hovers again, byte for byte 0.5.16. h_s1288 yaw: 90 % at 1.03 s (was 1.78), height ±0.01 m.
- **Stock 13.68 t and Buffed 16.5 t** (hover T/W under 1.00): the speed floor is unchanged (16.5 t: byte for byte with the switch off).
- **Rest of the 12-case set and regression set:** unchanged.

## The FDR HUD's "T/W above 2"

That number is not the jet's.

- **Measured:** in the same log, the climb at full dry thrust (41–42.5 s) gained about 1 m/s² of vertical acceleration, which means a T/W of about 1.1.
- **How the readout works:** it adds thrust magnitudes in every direction (nozzle + lift fan + roll posts). It rebuilds the fan's pitch term and the posts from your stick, not from the FCS's own fan and post commands.
- **Can't reproduce it:** the same sum on the bench reads 1.0–1.16, so I couldn't reproduce the 2. The F9 readout's Thrust line ("jet + lift") at that moment would show which part is inflated.

## Release

Main DLL 0.5.20. Release folder `C:\Games\NO mods\Vortex-FCS-0.5.20-release`.
