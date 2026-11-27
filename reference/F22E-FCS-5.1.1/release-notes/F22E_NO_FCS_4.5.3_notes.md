# F-22E Strike Raptor FCS 4.5.3: on a pitch release with lateral stick in, the TVC takes the pitch and the stabs stay on the roll

Base: 4.5.2. **Status:** compiled against the game's assemblies and benched on the calibrated plant with Tom's config. **Not flown.**

## The case (log F22E_FCS_20261123_202429, 4.5.2)

Tom, 23 Nov 20:31: the roll starts slowly after a pitch release at 40-60° AoA. 20:54: "if tvc has enough pitch authority then the stabs doesnt need to carry the pitch work and focus on lateral. No need for gates or anything, the allocator just figure it out."

The log has 31 pull → release → full-roll events at 340-600 km/h, released at 27-61° AoA with ~60 deg/s of pitch rate. Example at 99.9 s (497 km/h, 39° AoA):

| t (s) | what happens |
|---|---|
| 99.86-100.02 | The pitch stick is released. The lateral stab share (`latReserve`) drops from 0.45 to 0 in 0.1 s. It was gated on the pitch stick being held. |
| 99.89-100.12 | The nozzles slew out at ~75 deg/s and reach −18.8° for one tick. |
| 100.09-100.29 | The release capture's planned stop has nearly reached zero (qT 14 → 0) while the jet is still pitching at 41 → 10 deg/s. Its end-of-capture unwind (2.3.3) brings the nozzles home while the pitch is still running: −18.8° at 100.12, −10.4° at 100.26, +1.1° at 100.42. |
| 99.96-100.5 | The released-pitch priority (×30, scaled by the plan error) climbs to 28. The stabs pick up the pitch the nozzles dropped: mean stab deflection −1° → −20°. |
| | The roll is left to the flaperons, which are stalled at 40-55°. It sits at ~9 deg/s for 0.3-0.5 s. |

So the TVC was not out of authority. The law gave the pitch work to the stabs in three places: the pitch-stick gate, the ×30 priority, and a plan that ended before the jet did.

## Changes (inside the existing laws, no new device)

1. **No pitch-stick gate (`LatShareNoGate`).** The lateral stab share follows the lateral stick alone. It is a cost in the allocator: symmetric stab deflection gets dearer, so the nozzles take the pitch they can. When the nozzles can't carry the pitch, the stabs still do.
2. **The released-pitch priority yields to that share** in the same proportion, so it cannot take the stabs back.
3. **The braking plan follows the jet (`PlanFollowsJet`).** While braking a pitch rate toward the command, the plan may not run further ahead of the jet's rate than 0.1 s of its own deceleration, and never more than 8 deg/s. This is the same rule the 2.0.6 nose-on-runway law uses for the rotation. The nozzles now stay out until the pitch has actually stopped.

AoA/G limits, protection and unloads keep their own planning and full authority, as before.

- Switches: `LatShareNoGate` and `PlanFollowsJet`. With both off, all 31 log replays are identical to 4.5.2.
- IL diff vs 4.5.2: only `FcsController::.cctor`, `PitchLaw`, `Tick` and `CaptureRow` change length. The native core is unchanged.
- New telemetry column: `planHeld`, how far the braking plan was held back toward the jet this tick (deg/s).
- The dev config text for `LateralStabReserve` is updated to match.

## Bench results

### The 31 release-rolls from the 20:24 log

These are replays with the pitch following the log until the release. Run that way, the 4.5.2 bench matches the game (roll at 0.5 / 1 s: 12.6 / 35.0 vs 11.7 / 34.2 deg/s).

| | 4.5.2 | 4.5.3 |
|---|---|---|
| Velocity-vector roll at 0.5 s | 12.6 deg/s | 17.8 deg/s |
| Velocity-vector roll at 1 s | 35.0 deg/s | 38.6 deg/s |
| Max sideslip | 4.55° | 3.35° |
| Roll-curve dip (mean drop below its running max) | 2.8 deg/s | 2.1 deg/s |
| Max AoA after release | 50.8° | 52.6° |

The 0.3-0.5 s flat spot is gone at 30-50° releases. Examples:

| Event | Roll at 0.5 s | Roll at 1 s |
|---|---|---|
| 23.9 s | 13.9 → 33.7 | |
| 99.9 s | 18.5 → 29.6 | |
| 139.2 s | 4.6 → 20.7 | 28 → 38 |
| 184.5 s | | 27 → 41 |
| 263.3 s | | 32 → 46 |

At 55-61° AoA (91.8, 235.7, 256.2 s) nothing changes: the jet has little roll there in any law.

### Costs on the release-rolls

- **23.9 s:** the start is much faster, but there is a 3 deg/s dip at ~35 deg/s, and the roll at 1 s is 66 → 48 because the AoA stays ~2-3° higher.
- **Roll at 1 s, smaller losses:** 270.1 s 49 → 45, 147.8 s 28 → 24, 141.8 s 31 → 28 deg/s.
- **Roll at 0.5 s:** 178.4 s 42 → 34 deg/s.
- **AoA:** the pitch stops slightly later, so max AoA is up to 5° higher (115.0 s: 52 → 57°; 152.9 s: 44 → 48°; 34.3 s: 46 → 49°). The mean is +1.8°.

### Regression suite (reversals, corkscrew entries, reg set, utilisation, damage)

**Better:**
- pull-then-roll 400: sideslip 5.0 → 3.7°, peak roll 23 → 33 deg/s
- push+roll 200: sideslip 6.6 → 3.1°
- push-then-pullthrough+roll 450: sideslip 11.5 → 4.7°
- roll-then-pull reversal 400: sideslip 7.5 → 5.6°
- pull+roll reversal 450: time to 90 % 1.90 → 1.58 s, sideslip 17.3 → 15.7°
- one-fin pushover released 560: sideslip 6.4 → 1.4°
- Corkscrew entries at 400 km/h: early roll peak 50 → 56 deg/s (full throttle), 32 → 38 deg/s (throttle 0.15)

**Worse:**
- pull+roll reversal 556: sideslip 17.2 → 18.6°
- pull-then-roll 320: sideslip 2.4 → 4.5°
- 1 g roll reversal 500: sideslip 4.0 → 4.8°. The governor trims the roll command by up to 44 deg/s for 0.1 s just before the stop; roll rate is unchanged.
- Corkscrew entry 700 km/h, throttle 0.15: dip 46 → 39 deg/s
- No-fins pushover 600 (damage suite): AoA overshoot 65.3 → 68.0°

**Unchanged:** utilisation invariant (same 4 pre-existing FAILs, same numbers), the leaf cases and the remaining damage cases.

### Pitch-only releases (replays from the log, no lateral stick)

- 83.2 s: identical.
- 162.9 s (63° AoA): the stop eases in instead of undershooting (minimum pitch rate −1.4 → −0.3 deg/s). Time to under 2 deg/s goes 0.27 → 0.40 s, because the rate tails off around 2 deg/s.
- AoA max is the same in both.

## Not in this build

- The bench's pitch calibration at high AoA and high pitch rate. In free replays the bench jet loses ~11 deg/s of a 60 deg/s pull in the last 0.35 s before release; the game doesn't. That is why these results use replays pinned until the release.
