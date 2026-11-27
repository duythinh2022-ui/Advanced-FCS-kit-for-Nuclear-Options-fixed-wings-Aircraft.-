# Vortex FCS 0.4.1: high-speed airbrake roll / pitch fix, later STOVL hover hand-overs, ship-relative switch

17 Nov 2026. Built on 0.4.0 (the build from the other chat). Every 0.4.0 change is carried unchanged. Benched, not flown; the ship-relative mode is compiled only.

Tom's requests:
- 20:58: "it often switch to accel after 30kt something which is too slow ... if the ship is cruising at 30knts i want to do at least 40-45kts to catch up and land" (map ground speed). Picked: shift all hand-overs +20 kt.
- 21:01, on the ship frame: "does the irl ship do the same if so add a switch or a button in the keybind, that mode is off by default. If i'm in 800m of the ship, map gs to the moving ship."
- 21:05: "check latest log, i brake at 500-600kt+, the nose pitched down and the jet rolled to the left."

## 1. Airbrake at 500–600 kt

### The 20:43 log (flown on 0.3.15)

The brake was opened at 929.4 s: 615 kt (316 m/s IAS), idle, 250 m, stick centred.

| | 929.3 s | 931–934.5 s |
|---|---|---|
| Normal load | 1.16 g | 0.49–0.57 g, commanded 1.0 |
| Pitch rate | 0 | −0.7 to −1.0 deg/s (pitch attitude −1.6 → −4.8°) |
| Roll rate | 0 | −2.4 to −4.3 deg/s, bank −6.7 → −22° |
| Unmet roll (`unR`) | 0 | 20–26 deg/s² |
| Surfaces | — | frozen: rudders −19.83 / −19.83, outboard elevons +10.7 / +10.7, inboard 3.06 / 1.19 |

The second opening at 935.8 s (480 kt) built 4.7° of sideslip.

### Cause

The in-flight brake set (0.2.10) works like this:
- Both rudders flare 25° (`AirbrakeRudderFlare`).
- The outboard elevons take a trailing-edge-up share solved to cancel the flare's pitch.
- The pair is held to the set at 50 times the normal cost (`TeBrakeSymK`).

Above ~800 km/h the rudders' local AoA passes their attached-window limit before 25°. That limit is the settled window's: lift peak minus `SettledStallMargin`, or the slope knee, whichever is lower.
- Both rudders therefore sat on their window edge.
- No yaw differential was left that did not break the set.
- The allocator kept a solve that missed its roll and yaw targets rather than pay the set's cost.

On the bench at 900 km/h I priced moves at the stuck point (3.0 s after the brake opened):
- **Chosen solve:** cost 3.58, moment error −20.7k / −12.7k / +23.2k N·m (roll / pitch / yaw).
- **Right rudder 3° less flare:** error −11.2k / −6.6k / +2.4k, but cost 4.19.

### Bench reproduction (0.4.0, Tom's config, brake opened at 0.55 s, idle, 250 m, stick centred)

| Start IAS | g min / mean | Bank after 6 s | Max sideslip |
|---|---|---|---|
| 1138 km/h | 0.44 / 0.86 | 15.0° | 4.2° |
| 900 km/h | 0.30 / 0.53 | 21.5° | 2.9° |
| 700 km/h | 0.84 / 0.95 | 0.3° | 0.1° |

The bench shows the same pattern as the log: roll left, low g, from about 800 km/h up.

### Change: `AirbrakeRudderFlareWindow` (Dev, `7. Airbrake`, default true)

- **Flare limit:** the flare is held to what both rudders reach inside that window, less one grid step (1.17°) each way for yaw.
- **Pace:** the limit moves at the set's own pace (the full flare in about 1 s).
- **Elevon share:** the outboard trailing-edge-up share is solved against the same flare.
- **false** = 0.4.0.
- No allocator, law or native change; ABI 2013001.

### Bench: 0.4.0 → 0.4.1 (brake held 5.5 s, stick centred)

| Start IAS | g min / mean | Bank after 6 s | Max roll rate | Max unmet roll | Rudders | IAS at 6 s |
|---|---|---|---|---|---|---|
| 1138 km/h | 0.44 / 0.86 → **0.69 / 0.93** | 15.0 → **0.0°** | 7.0 → 0.0 deg/s | 52.6 → 3.5 deg/s² | −15 / −22 → −14 / −14 | 852 → 859 km/h |
| 900 km/h | 0.30 / 0.53 → **0.77 / 0.95** | 21.5 → **0.0°** | 6.6 → 0.0 | 114 → 0.2 | −17.5 / −22 → −14 / −14 | 723 → 722 |
| 700 km/h | 0.84 / 0.95 → 0.84 / 0.96 | 0.3 → 0.0° | 0.3 → 0.0 | 2.6 → 0.0 | −17.5 / −17.5 → −14 / −14 | 589 → 591 |
| 500 km/h | 0.94 / 1.02 → 0.94 / 0.99 | 0.1 → 0.0° | 0.2 → 0.0 | 2.3 → 0.0 | −17.5 / −17.5 → −14 / −14 | 437 → 440 |

Notes on this table:
- The g dip on opening at 1138 km/h (0.69) is the 1 s deployment transient. It was 0.44 in 0.4.0.
- The flare is 14° at every speed tested, against 17.5° (bench) or 19.8° (game) before. Speed lost is within 7 km/h in 6 s.

### Bench: brake out with full roll then a 0.6 pull

| | 900 km/h, 0.4.0 → 0.4.1 | 500 km/h, 0.4.0 → 0.4.1 |
|---|---|---|
| Roll rate max | 250 → 250 deg/s | 179 → 179 |
| Sideslip in the roll | 1.42 → 0.94° | 1.71 → 1.64 |
| Pull g max | 6.15 → 6.29 | 4.05 → **3.91** |
| Sideslip in the pull | 2.89 → 0.99° | 1.51 → 1.40 |
| Bank drift after the pull | −12.4 → 1.3° | 0.0 → −0.5 |

The CTOL case set doesn't use the brake; with the brake stowed the new code returns the 0.4.0 flare, so those cases weren't run again.

## 2. STOVL hover hand-overs 20 kt later

### Change: `StovlRegimeShift` (main config, `1. General`, default 20 kt, 0 = 0.4.0)

All of these move together:

| Hand-over | 0.4.0 | 0.4.1 (midpoint) |
|---|---|---|
| Lever: ground-speed command → speed command (HUD `+xx KT` → `ACCEL` / `HOLD SPD` / `DECEL`) | 25–35 kt ground speed (30) | 45–55 (50) |
| Stick and pedals: hover laws → forward-flight laws | 30–40 kt (35) | 50–60 (55) |
| Released-stick side-velocity hold | 15–30 kt (22) | 35–50 (42) |
| CTOL blend (lever = thrust, HUD `THR %`) | 40–60 kt airspeed (50) | 60–80 (70) |

The roll-rate schedule (100–130 kt) is unchanged. The lever scale is unchanged: the centre detent is +30 kt, and +45 kt is at about 0.65 lever.

### Bench (0.4.0 STOVL case set, shift 0 → 20, Tom's config)

- **Hover cases:** side stick, tap, pedal, fore-aft step, side + fore-aft, gust, climb / sink release, vertical take-off are identical in every column.
- **New case** (vertical take-off, lever to 0.65 from 18 to 60 s, then the aft detent):

| | 0.4.0 | 0.4.1 |
|---|---|---|
| Lever 0.65 holds | 54 kt (went to the speed command) | **46 kt ground speed**, as commanded |
| Height in the run | 18.5–18.7 m | 18.49–18.50 m |
| Stop from the run to under 2 kt | ~12 s | ~5 s (pitch 17.5° on the stop) |

| Case | 0.4.0 | 0.4.1 |
|---|---|---|
| 30 kt bank stick (0.3 for 3 s) | 19.1° bank, **24.5° sideslip**, 26.7 deg/s roll | 10.2° bank, 9.1° sideslip, 9.5 deg/s (hover law: a side translation) |
| Lever to 0.6 ("38 kt") | ran to 51.7 kt, height −4.7 m, pitch to 8.6° | holds 40.6 kt, height ±0.02 m, pitch 3.6° |
| 40 kt bank stick | turns, 0.1° sideslip | translates sideways: **8.5° sideslip**, 12.4° bank |
| Deceleration after a 380 km/h conversion: under 1 kt | 93.0 s | 85.4 s |
| Height span under 150 km/h | 17.6 m | 8.3 m |
| Pitch max in the deceleration | 26.0° | **33.2°** (at 24 kt, AoA 33°) |
| Short take-off, same stick (0.5 for 3 s at 30 kt) | 15 s: 53 kt / 26 m / 12.9°; 20 s: 81 kt / 83 m / 18.6° | 15 s: 73 kt / **12 m** / 5.4°; 20 s: 124 kt / **28 m** / 9.3° |
| Short take-off lift-off / ground run | 5.07 s / 44.8 m | 5.12 s / 46.0 m |

**Worse:**
- **Short take-off:** below 60 kt the stick is the hover law's climb command, not a rotation. The same input climbs to 12 m instead of 26 m by 15 s, and the jet leaves faster and lower.
- **Deceleration:** the hover stop starts from 55 kt instead of 35. It is 7.6 s quicker with half the height span, but the nose goes to 33° (26° before).
- **Bank stick between 35 and 55 kt:** it translates instead of turning (8.5° sideslip at 40 kt).

## 3. Ship-relative STOVL speeds: a switch, off by default

**Does the real ship / jet do this?**
- The F-35B shows a ship-referenced velocity vector in the helmet for rolling vertical landings (FlightGlobal on the QE-class trials).
- I found no source that its hover laws hold ship-relative speed.
- Ship-relative translational rate command with position hold is a research mode for ship-based rotorcraft (Penn State, DLR / ONERA, NLR papers).
- So: a switch, off by default.

**What changed:**
- **0.4.0:** inside 500 m of the nearest friendly ship the speeds were always ship-relative. The ship's speed was refreshed every 3 s and stepped in and out at 500 m.
- **0.4.1:**

| Main config (`1. General`) | Default | |
|---|---|---|
| `StovlShipRelative` | false | false = map ground speed at every distance |
| `StovlShipRelativeRange` | 800 m | full inside 600 m, faded to zero at 800 m |
| `StovlShipRelativeKey` | unbound | toggles the switch in flight; HUD message `STOVL SPEEDS: SHIP RELATIVE` / `MAP` |

- **Ship velocity:** the nearest ship is found every 3 s; its velocity is read every tick.
- **Fade:** the ship's share moves at most 1/3 per second, so a key press or a lost ship never steps the speed reference.
- **HUD:** while ship-relative, the lever readout ends in ` SHP` (e.g. `+15 KT SHP`).
- **When on, it covers:** the lever's ground-speed command, the hover hand-overs and the released-stick position hold.

**Things to know:**
- With the switch off (default), the released-stick position hold over a moving deck holds a map spot. Hold `+xx KT` to match a moving ship.
- With it on, the lever's `+xx KT` becomes relative to the ship as the fade comes in. A +45 kt map command becomes +45 relative (about 75 kt over the map with a 30 kt ship), so bring the lever back as you close.
- None of this is benched: the bench has no ship.

## 4. Not done / not checked

- Flown in game: nothing.
- The ship mode: compiled only.
- Short take-off technique with the new bands: a pull at 60–80 kt is where the rotation now starts. Not benched.
- The brake at high AoA, or with stores: not benched.
