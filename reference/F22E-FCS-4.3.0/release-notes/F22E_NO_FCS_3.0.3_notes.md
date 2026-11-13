# F-22E FCS 3.0.3: the asymmetric-load cap comes down faster after a part is shot off

Base: 3.0.2. Patch bump: one internal change, also in Compass 0.1.9. Compiled; benched; **not flown**. 3.0.2 is kept as `.dll.3.0.2.bak`.

## Tom (5 Nov, 21:21)
"check new logs" (`F22E_FCS_20261105_210954.csv`, 3.0.2)

## What the log shows
**The damage:** both fins at ~50 s, the right stab (`Elevator_R`) at 260.6 s. Gear down from ~338 s.

**3.0.2's fixes held:**
- **Straight flight with no fins, 60–123 s, up to 420 m/s:**
  - Sideslip ≤ 0.6°.
  - No jumps in the yaw command.
- **Every stick-centred stretch below 10° AoA:**
  - Roll rate median ≤ 0.8 °/s.
  - No roll creep: bank drift 0–7° over 3–30 s.

**1. 260.6–270 s: the right stab shot off, full aft 2 s later.**
- **The cap was too slow:**
  - The asymmetric-load cap's target dropped 65 → 12.6° at once.
  - The cap followed at its 10 °/s tighten rate, so it was still at 45° when the pull came.
- **What the jet did:**
  - AoA reached 25° at 3.4 g.
  - The lone left stab's load and the yaw it made took the jet through 78° of bank, with full left aileron and flaps against it.
  - Sideslip −19°.
  - The cap reached 12° only at 266 s.

**2. 194–212 s: no fins, idle, a 9 g roll reversal then a full push to −20° AoA.**
- **What happened:** the yaw rate stayed at +14…+21 °/s while every yaw effector was asked for more, the command 80–110 deg/s² of yaw acceleration. Sideslip went to −47°.
- **The cause:** with no fins at negative AoA the yaw axis is about 13 deg/s² both ways.
- **Not changed.**

**3. 338–358 s, the end: gear down, no fins, no right stab.**
- **What happened:** a full left roll at −74 °/s, then a pull. The yaw went the wrong way (+11 °/s, adverse) with nothing to stop it. Sideslip −30°, then departure.
- **Not changed.**

## The change: `AsymCapGapTighten`
- **What it does:** the tighten rate (`AsymCapTightenRate`, 10 °/s) is multiplied by gap / 3°, between 1× and 4×. A large drop now comes down at up to 40 °/s.
- **What it leaves alone:** gaps under 3°, and caps that are rising.

## Bench: a part shot off 1 or 2 s before full aft (400 / 470 / 560 km/h)
| Damage | Case | 3.0.2 | **3.0.3** |
|---|---|---|---|
| Right stab | AoA peak | 26–43° | **22–25°** |
| No fins + right stab | AoA peak | 26–44° | **22–27°** |
| No fins + right stab | Peak sideslip, 400 km/h, 1 s / 2 s | 26.1° / 7.2° | **1.5° / 0.4°** |
| No fins + right stab | Peak sideslip, 470 km/h, 1 s / 2 s | 8.9° / 2.4° | **2.3° / 0.5°** |
| Left mid-wing, with or without fins | | unchanged | unchanged (no large cap drop there) |

## Regression suite
- **Clean jet:** reversals, corkscrews, the regression set and the utilisation invariant are identical.
- **Damage suite, the only lines that move are the one-fin cases** (fin off at t = 0, so the cap now settles faster):

| Case | 3.0.2 | **3.0.3** |
|---|---|---|
| Leaf stop 296: peak sideslip | 3.0° | 4.7° |
| Leaf stop 370: peak sideslip | 2.5° | 2.9° |
| Leaf reversal 296 / 370 | 5.6 / 5.5° | 5.7 / 4.7° |
| Push-over held 560 | 6.1° | **4.2°** |
| Fins + right flaperon roll reversal at 890 km/h | departs | departs |

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 3.0.3; 3.0.2 kept as `.dll.3.0.2.bak`.
- **Dev unlock:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 3.0.3.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.0.3-release`.
