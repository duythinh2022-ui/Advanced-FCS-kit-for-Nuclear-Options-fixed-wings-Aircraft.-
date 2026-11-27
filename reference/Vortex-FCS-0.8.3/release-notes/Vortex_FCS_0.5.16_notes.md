# Vortex FCS 0.5.16: lift correction, wing lift in the budget, speed floor when too heavy to hover, caution box, smooth touchdown

19 Nov 2026. On 0.5.15.

**Status: benched (13.8 t Buffed unless a weight is given), not flown. The caution box has never been seen on a screen.**

Five changes, each behind its own switch, all on by default. With the four law switches off the bench output is byte for byte 0.5.15's (12 cases).

Tom, 19 Nov:

> is it a physical trade off or control laws

> also the wing lift term, would it be beneficial to that?

> or it blocks the jet at airspeeds that relies more on the t/w that it doesnt have to be on air

> no, i need it to be the dialog on top of the screen like the stovl conv messages ... then make a mod built into the fcs mod that allows that

> green then

> also add a feature that for vertical touch down, currently that the jet will go as fast as long as the gears wont snap, but the jet still bounces. So right before touch down, it slows down just right enough that the contact is smooth, but dont be too conservative or too relaxed, just right enough. Make it model based, like weight and availible authority

## Three things that did not come out as the findings doc said

1. **The lift correction as proposed made the release worse.** Using the lift everywhere (bank targets included) took the side speed after the release from 0.82 to 1.34 m/s: the lift is cut for only ~1.5 s while a climb is stopped, the bank chased it up to 29° and was still there when the lift came back. What is built is different (below).
2. **The wing-lift term does not give the climbing turn its rate back.** The doc forecast 13.4°/s kept at 30 kt with a climb. Measured: 9.4 → 9.8°/s. In a 6 m/s climb at 30 kt the flight path points 21° up, the wing's angle of attack is negative and it carries nothing. The term helps level flight (below). The 10 against 13.4°/s with a climb stays a physical limit, split evenly as you chose.
3. **"Too heavy to hover" on the law's scale is not the same as physically unable to hover.** The law's T/W counts 95 % of full dry thrust (5 % is kept for the yaw nozzle and the roll posts). Between 0.95 and 1.00 on that scale 0.5.15 still hovers, with nothing in hand. You chose the law's scale (hover with manoeuvring margin); see the end.

## 1. Lift correction (`StovlLiftCorrect`, dev file, on)

The law converted between a bank and the sideways acceleration it gives as if the lift were always the weight. It now uses the lift itself (g + the vertical acceleration), in two ways:

- **Lift above the weight** (a climb accelerating, a sink being stopped; lasts seconds, at most the spare thrust): the bank is planned on the lift, so less bank is asked for the same sideways acceleration.
- **Lift below the weight** (a climb being stopped, a descent starting; deep but over in ~1.5 s): the bank target stays where it was. The nose's turn rate is cut to the lift's share instead, because the yaw axis is the fast one. With the pedal let go the nose's rate only falls.

No new gain. With no vertical acceleration nothing changes.

Side speed at the pivot (m/s), pedal turn with a full climb started 0.5 s before and released with the pedal:

| | Phase | 0.5.15 | 0.5.16 |
|---|---|---|---|
| 30 kt | climb accelerating | 0.31 | **0.06** |
| 30 kt | climb at its rate | 0.30 | **0.07** |
| 30 kt | after both released | 0.82 | **0.26** |
| 45 kt | climb accelerating | 0.26 | 0.11 |
| 45 kt | climb at its rate | 0.13 | 0.04 |
| 45 kt | after both released | 0.58 | 0.14 |
| 30 kt | climb released, pedal held | 0.80 | 0.26 |

- **Curve at the release (30 kt):** the nose's rate falls 14.1 → 0 °/s in 2.4 s with no reversal: −9 to −10 °/s² for the first 0.8 s (the lift cut), then easing to about −4 °/s². The bank comes out from 21° without rising first.
- **Turn rate once the climb has its rate:** 12.8°/s against 13.4 (4 % lower). 0.5.15's 13.4 was flown with the 0.30 m/s of skid.
- **Side stick with a climb:** 0.4 stick peaks at 1.54 m/s for 1.50 commanded (0.5.15: 1.67, the item left open in 0.5.14). Full stick: 3.95 for 3.81 (4.01); past zero at the stop 0.05 m/s (0.09).
- **Heading kick at bank reversals with a side stick and a climb (open from 0.5.14, not targeted):** 0.4 stick 6.5° → 4.7°, 11.4 → 9.3 °/s; full stick 1.0° → 0.8°, 2.6 → 2.2 °/s. Smaller, still there, cause still not isolated.

**Not fixed, found on the way:** with the pedal held while a descent is stopped, the sink stop takes the whole thrust budget (by design), the turn nearly stops for ~3 s and 0.6–0.7 m/s of side speed builds (0.5.15: 0.65–0.69; 0.5.16: 0.56–0.62). The pedal's rate limit follows the budget through a 1 s low-pass and is late. Same in 0.5.15.

## 2. Wing lift in the budget (`StovlWingLiftBudget`, dev file, on)

The translation budget took the whole weight as the thrust's to carry at every speed. It now takes off what the wing carries: measured (mass × (acceleration − gravity) − the propulsive force, vertical part, 0.3 s low-pass), never below zero, never more than the wing could make at `StovlWingAoAMax` at that airspeed. In the hover that is zero.

| | 0.5.15 | 0.5.16 |
|---|---|---|
| 45 kt pedal turn, level: turn rate | 9.1°/s | **10.7°/s** (bank 20.6 → 24.3°) |
| 45 kt pedal turn: side speed at the pivot, turn / after release | 0.02 / 0.08 | 0.05 / 0.10 m/s |
| 30 kt pedal turn, level: turn rate | 14.4°/s | 14.5°/s (already the pedal's full rate) |
| 30 kt turn, first 4 s of a climb | 9.4°/s | 9.8°/s |
| Full lever from the hover: to 100 / 150 km/h | 6.23 / 8.70 s | **5.83 / 8.18 s** |
| Same run: height band up to 200 km/h | −0.15 / +0.03 m | −0.22 / +0.04 m |
| Stop from ~65 kt (mission case): nose-up peak | 24.2° | 28.6°, at rest ~1.5 s sooner, height within ±0.05 m |
| Hover pedal, hover side stick | | identical / within 0.03° of bank |

- The stop from speed is harder because the wing's lift counts while there is speed; the nose goes higher for it. You have the tilt ceiling off, so nothing caps it.
- Measured wing lift, level, 13.8 t: 3.7 kN at 30 kt, 9.2 kN at 45 kt (the findings doc: 3.9 and 9.7).

## 3. Speed floor when too heavy to hover (`StovlMinSpeedFloor`, dev file, on)

**What 0.5.15 does today (bench, Buffed, lever at zero ground speed after a conversion at 250 km/h):**

| Weight | Hover T/W (law's scale) | 0.5.15 |
|---|---|---|
| 16.5 t | 0.98 | Slows all the way to a hover at 96 % of full dry thrust. Takes 160 s from 55 kt (0.2 m/s², the budget's floor). Height held. |
| 17.5 t | 0.935 | Slows to ~15.5 kt, then sinks at up to 0.1 m/s (2.6 m in 40 s) with the speed creeping back to 16.4 kt. |

**What 0.5.16 does:** while the hover T/W is under 1.00 (held until it is back over 1.02), the lever's deceleration stops at the lowest airspeed at which thrust and wing together carry the weight with 2 % in hand, and holds it.

- **Reserve:** 2 % of the weight, which is the margin the latch lets go at, so the floor is exactly zero when the caution clears. A larger reserve would have put a floor on jets that hover fine today.
- **Wing model:** lift area at zero AoA learned from the measured lift in level flight, plus `sCLa` × the attitude the laws hold at that speed. No new gain.
- **In the air only.** On the wheels the lever is the lever, so a rolling landing stops and a short take-off starts from rest.
- **HUD:** `LOW HVR T/W: STO/RVL ONLY` in the caution box while latched (from the conversion request on; the T/W is worked out from the lift system's geometry before the fan runs). The throttle gauge reads `MIN 39 KT` while the floor holds the lever.

| Weight | T/W | Floor | Held at | Height / vertical speed while held |
|---|---|---|---|---|
| 16.0 t | 1.012 | none | hovers | as 0.5.15 |
| 16.5 t | 0.983 | 39.3 kt | 39.38–39.42 kt | ±0.00 m / ±0.001 m/s; thrust command 0.74 (it tops out at 0.81) |
| 17.5 t | 0.935 | 56.4 kt | 56.42 kt | ±0.00 m / ±0.000 m/s |

- **Arrival at the floor, 17.5 t:** one smooth deceleration, no undershoot.
- **Arrival at the floor, 16.5 t: not clean.** The floor's first estimate is 5 kt high (learned at the higher attitude the jet flew at speed); the jet pauses about 1 s at 44.8 kt, the estimate comes down as the wing is measured level, and the jet goes on to 38.0 kt (1.3 kt under) before settling at 39.4 kt, 8 s after the pause. One reversal of the fore-aft acceleration (+0.5 m/s²). Height within 0.1 m during the arrival.
- **Rolling landing, 16.5 t:** descent at 1.5 m/s on the floor (37.7 kt), touchdown with no wheel off again, the floor lets go on the wheels.
- **Hover T/W figure:** 1.138 asked from CTOL, 1.141 in the hover; a full pedal input moves it 0.001. It is held while converting and on the wheels (there the geometry it is worked out from reads 4 % low).

**Not tested:** wind, hot or high conditions, ship-relative mode, side stick or pedal turns at the floor, a floor above the CTOL blend (~65 kt and up, where the lever is thrust and the floor does not apply), lopsided loads.

## 4. Caution box (`CautionBox`, player file, on; `CautionColor`, Hud)

A box of the FCS's own directly under the game's message line: a copy of the game's box (its background and label are cloned, so font, size and background are the game's). A caution is a line in it for as long as its condition lasts and is gone the frame it ends.

- **Placement:** every frame from the game box's rendered bounds (its rectangle and its text's bounds, whichever reaches lower), one line's height below. While the game's line is empty the caution box takes its place.
- **Colour:** the game label's colour, which follows the HUD colour setting (`CautionColor = Amber` for amber).
- **No onset line** on the game's message line. `UNSAFE STOVL - DROP RIGHT/LEFT WPN` stays a 4 s message.
- **Hidden** when the game's own box is out of the hierarchy. Cleared at a new mission. Player's own jet only.
- **`CautionBox = false`:** no box; the caution is sent once as an ordinary 4 s message.
- First and only caution: `LOW HVR T/W: STO/RVL ONLY`.

**Not verified (cannot be, offline):** everything visual. In particular which of the game's two objects is the other's parent (the code handles the three cases, none seen), which way the game's box grows, the gap, behaviour at other HUD scales, and next to Third Person HUD Plus and Tactitools. If it misbehaves, `CautionBox = false` takes it out without touching the laws.

## 5. Smooth touchdown (`StovlSmoothTouchdown`, dev file, on)

**Before (bench, reproduces what you see):** the descent ran at `StovlSinkRate` (5 m/s) into the runway, because the gear-break limit is above that. Contact at 5.0 m/s, 3.4 g, the wheels off again for 0.66 s and 0.24 m high.

**Now:** the descent is slowed to the highest sink rate at which no wheel that has touched leaves the ground again.

- **Where the rate comes from:** the whole jet dropped on its legs in heave and pitch (the game's own leg force, its spring and damper rates, the mass and pitch inertia, the CG's place between the legs, the hover's pitch attitude, the engine spooling down from the hover's thrust). Recomputed when the mass moves 1 %.
- **Where the slow-down starts:** the braking envelope that was already there, on 70 % of the spare lift, so a heavy jet starts higher. Its lag is now the vertical axis's own thrust response time (0.25 s here) in place of a fixed 1 s; with 1 s the last 2 m were flown at the touchdown rate.

| | Contact | Peak g | Wheels off again | Settled after contact |
|---|---|---|---|---|
| 13.8 t, 0.5.15 | 5.00 m/s | 3.4 | 0.66 s, 0.24 m | 2.08 s |
| 13.8 t, 0.5.16 | 1.93 m/s | 1.3 | none | 1.02 s |
| 11.0 t | 1.87 m/s | 1.5 | none | 1.00 s |
| 12.5 t | 1.66 m/s | 1.3 | none | 1.02 s |
| 15.0 t (spare lift 0.69 m/s²) | 2.08 m/s | 1.4 | none | 1.03 s |
| Stock 12.8 t (spare lift 0.51 m/s²) | 1.94 m/s | 1.5 | none | 1.02 s |

- **Model's rate:** 1.98–2.12 m/s over 11–15 t.
- **How close to the edge:** on the bench plant, landings forced at 2.15 and 2.3 m/s do not hop; 2.5 m/s hops 2 mm for 0.03 s. So the model sits about 15 % inside the plant's edge.
- **Time:** contact comes 1.2 s later than in 0.5.15 from 33 m (32.8 against 31.6 s); the jet is at rest at the same time (33.8 against 33.7 s), because the bounce is gone.
- **Flare curve, 13.8 t:** 5.0 m/s held to 12 m above the runway, then one steady 0.9 m/s² deceleration to 2.0 m/s at 0.8 m, then 2.0 m/s to contact. The vertical speed follows the envelope within 0.02 m/s. 15 t: the same shape at 0.47 m/s².
- **Gear compression in the regression landings:** 0.36–0.43 m → 0.13–0.17 m (break at 0.5).

**Not verified:** that the game's legs behave as the bench's (the rates are the game's: mains 400 kN/m and 40 kN s/m, nose 200 and 20); a deck that moves; touchdowns with side speed or bank. Not flown.

## Checks

- **Switches off = 0.5.15:** `StovlLiftCorrect`, `StovlWingLiftBudget`, `StovlMinSpeedFloor`, `StovlSmoothTouchdown` all false: 12 bench cases byte-identical to a pristine 0.5.15 build (118 columns each): 30 and 45 kt pedal turns, the 30 kt turn with a climb, side stick, side stick with a climb, lever, lever with a climb, hover pedal, full lever to CTOL, side + fore-aft together, Stock 12.8 t stop + side stick, vertical landing.
- **Regression set (`regr2.sh`):** hover cases at four weights and the vertical take-off unchanged; the five landings as above; full-throttle short take-off: lowest height the same, 355 kt at the end against 358; mission case: nose-up peak 29° against 24° (section 2).
- **Build:** offline smoke test passes (158 types, 0 errors); the source zip rebuilds the DLL byte for byte.
- **Bench scripts added:** `bench/scripts/vortex/` `k30run.sh`, `k30an.py`, `axrun.sh`, `axan.py`, `td.py`, `hv3.sh`, `hv4.sh`, `idset.sh`, `cmp.py`. Bench CSV: `gLift`, `wingLift`, `twHover`, `vMinFloor`, `vTouch`.

## Decided: where the "too heavy to hover" line sits

Tom, 19 Nov (asked whether the line should be 1.00 on the law's scale or 0.95, the true T/W of 1.00):

> to heavy to hover with maneuvering margin

So the line stays as built: 1.00 on the law's scale, which counts 95 % of full dry thrust and keeps 5 % for the yaw nozzle and the roll posts. The caution and the floor come on when the jet can no longer hover *with that margin* (Buffed: from about 15.8 t), not only when it cannot hold its height at all (about 16.6 t). No code change (`LowTwOn` 1.00, `LowTwOff` 1.02).

## Config

- Dev file, `[9. STOVL]`: `StovlLiftCorrect`, `StovlWingLiftBudget`, `StovlMinSpeedFloor`, `StovlSmoothTouchdown` (all true).
- Player file, `[1. General]`: `CautionBox` (true), `CautionColor` (Hud).
- No migration.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.16 and `meta.json`. 0.5.15 kept as `Vortex_FS20_FCS.dll.0.5.15.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.16-release`.
- Restart the game to load it.
