# Vortex FCS 0.5.19: touchdown bounce after a push-down from a climb; heavy jet cannot go vertical

20 Nov 2026. On 0.5.18.

**Status: benched, not flown.**

Tom, 20 Nov:

> check the 3 latest log, ignore the middle one. one of them was the bounce test. It worked but not 100%, sometimes it will still bounce, check it if it is the fcs or the physical limit. The latest one was with very heavy load, on my fdr hud, i can see the twr can reach above 2 in stovl but i cant go vertical?

## 1. Bounce: the FCS, not the gear (fixed)

Log `Vortex_FCS_20261120_011920.csv`, 12.1 t. The smooth-touchdown target at this weight is 1.91 m/s.

Every landing from a descent, split by how the jet reached the braking envelope:

| Touchdown | Entered the envelope at | Accel at entry | Thrust at full (of last 1.5 s) | Contact sink | Rebound |
|---|---|---|---|---|---|
| 69.4 s | −4.96 m/s | +0.1 m/s² | 0.2 s | 1.46 m/s | none |
| 82.3 s | −4.94 | −0.2 | 0.8 s | 1.57 | none |
| 112.6 s | −4.80 | −0.7 | 0.7 s | 1.45 | none |
| 178.6 s | −4.97 | 0.0 | 0.75 s | 1.48 | 0.26 m/s |
| **102.4 s** | −3.66 | **−3.5** | **1.4 s** | **2.67** | **0.81 m/s** |
| **135.1 s** | −4.54 | **−1.4** | **1.3 s** | **2.16** | **0.72 m/s** |
| **167.3 s** | −4.05 | **−2.8** | **1.35 s** | **2.41** | **0.80 m/s** |

**Cause:** the three bounces are the landings where you pushed down out of a climb. The jet reached the braking envelope still accelerating downward. Before the thrust could brake, it first had to cancel that acceleration. It ran at full for about 1.35 s and still touched at 2.2–2.7 m/s.

The thrust was the same in every landing. The jet had enough; the braking simply started too late. So this is the FCS, not a physical limit.

**Why the envelope missed it:** the envelope works out the allowed sink rate at each height from two numbers: the thrust's response time τ, then a braking deceleration aStop. It assumed the descent was steady when braking began.

**Fix (`StovlTouchdownAccel`, dev file, on; false = 0.5.18):**

- A downward acceleration aDn takes τ × aDn / aStop longer to cancel, and the sink keeps growing during that time.
- So the sink allowed now is the one that lands on the envelope after that delay.
- aDn is the measured vertical acceleration (0.1 s low-pass).
- Once the jet is on the envelope it is braking, aDn is 0, and nothing changes.

**Bench** (Buffed 12.1 t, full climb, then full push down; 7 climb lengths):

| | Contact sink | Wheels off |
|---|---|---|
| 0.5.18 | 1.64–2.27 m/s; two bounces (2.06, 2.27) | twice, up to 0.037 m |
| 0.5.19 | 1.65–1.86 m/s | never |

The descent now peaks at about −3.6 m/s instead of −4.6 m/s, and the jet reaches the ground 0.4–0.7 s later. That is the cost.

**Other sets:**

- **12-case set:** only L changes (heading 0.3°, contact 1.93 → 1.74 m/s).
- **Regression set:** only the vertical landings change (gear strokes within 0.009 m, touchdown sink within 0.01 m/s).

**Still in the log:** small rebounds of about 0.3 m/s at contact sinks of 1.5–1.8 m/s (178.6 s and 143.6 s). From 0.3 m/s the jet rises about 5 mm, which you won't see. The bench shows no wheel leaving the ground at 1.86 m/s. So the game's gear may let go a little below the 1.9 m/s the model works out, but at these sizes I can't tell from this log.

## 2. Heavy jet cannot go vertical: the FCS speed floor (not changed, your call)

Log `Vortex_FCS_20261120_012539.csv`, 15.15 t:

- **Hover T/W on the law's scale (95 % of full dry):** 1.054. Physical vertical T/W at full dry ≈ 1.11.
- **The jet can hover.** At 29.5 s it lifted straight off the ground on the stick, climbing at 3.3 m/s.
- **But the FCS sent it forward.** At once the ground-speed command went to 34.6 kt (the speed floor) with your lever at the zero-speed detent, and the jet accelerated to about 33 kt and stayed there.
- **Why:** the 0.5.17 rule, `StovlManeuverTilt` 20°, counts a jet as too heavy to hover when it can't tilt its lift 20° and still hold height. At 1.054 the most it can tilt is acos(1/1.054) = 18.4°, only 1.6° short. So the caution latched (hover T/W × cos 20° = 0.99 < 1) and the floor took over.

**About the FDR HUD's "above 2":** that number is not physical.

- **Measured:** the climb at 41–42.5 s (full stick, full dry, 33 kt) gained about 1 m/s² of vertical acceleration. That fits a T/W of about 1.1.
- **How the FDR's T/W works:** it adds up thrust magnitudes (nozzle + lift fan + roll posts), whatever way each points. It rebuilds the fan's pitch term and the posts from your stick, not from the FCS's own fan and post commands.
- **Can't reproduce it:** the same sum on the bench reads 1.0–1.16, so I can't reproduce the 2. The F9 readout's Thrust line ("jet + lift" split) at the moment it reads 2 would show which part is inflated.

## Release

Main DLL 0.5.19. Release folder `C:\Games\NO mods\Vortex-FCS-0.5.19-release`.
