# Vortex FCS 0.5.6: pedal pirouette, and the STOVL hand-overs 10 kt later

18 Nov 2026. On 0.5.5.

**Status: benched, not flown.**

Tom, 18 Nov:

> can you shift the threshold for stovl to have ctol like control and accel 10 more knots faster. Also, why when i go 40-50kt something and then i yaw, the jet follows a curve instead of yawing at max available yaw rate, fix that

You chose the flat pirouette. On the shift you said the ACCEL shows from about 50 kt in play, so everything moves 10 kt later.

## 1. Pedal pirouette

**Why it curved.** Since 0.4.7, under your rule that the FCS doesn't let the pilot command what the jet can't hold, the pedal at speed asked for a coordinated turn. The nose was only allowed to turn as fast as the bank could swing the velocity round with it: about 7.5 deg/s at 45 kt on your jet. That limit is what you saw as a curve.

**Now** (`StovlPedalPirouette`, on, in your player config). Up to the stick and pedal hand-over (now 60–70 kt):

- While the pedal is held, the nose turns at the yaw axis's full rate and the jet keeps flying the way it was going.
- The lever's speed stays along the direction the nose had when the pedal came in. The side stick still moves the jet across the present nose.
- No bank goes in to fight the turn.
- When you let go, the yaw stops. The lever's speed then runs along the new nose through the normal start and stop profiles, so any skid or backwards motion is taken out at the usual rates.
- With airspeed, the nose is held short of the sideslip where the airframe's weathervane would take 80 % of the yaw authority (`StovlPirouetteSlipShare` in the dev file). At 30–45 kt the bench airframe's weathervane is weak enough that this never bound: the nose went all the way round.

Bench, your jet (Buffed, 13.8 t):

| Case | 0.5.5 | 0.5.6 |
|---|---|---|
| 45 kt, full right pedal 8 s: yaw rate | 7.2 deg/s | 23–27 deg/s |
| — heading / ground track turned | +54° / +51° | +180° / 0° |
| — bank / height | 20° / ±0.02 m | 5° / −0.06/+0.03 m |
| — after release (nose 180° off the track) | — | stops, then 45 kt along the new nose in 13 s, height −0.03/+0.05 m, bank ≤ 12° |
| 30 kt, full right pedal 8 s: yaw rate | 11.4 deg/s | 22–25 deg/s |
| 45 kt, half pedal 2 s: yaw rate / heading | 3.7 deg/s / +7° | 11.8 deg/s / +23° |
| — after release | — | the 8.6 m/s skid taken out in 3.5 s at up to 22° of bank, height ±0.05 m |
| Stock 12.8 t, 45 kt, full pedal 5 s: yaw rate | 3.7 deg/s | 11 deg/s |
| Hover, full pedal 3 s | 23.7 deg/s peak, 69.3° | 23.7 deg/s peak, 69.4° |

Yaw stop after release: 14° of overshoot from 26 deg/s at 45 kt, 3° from a half-pedal turn. That is the yaw axis stopping at its authority, the same as in the hover.

**What to expect.**
- A long full pedal at 30–45 kt can take the nose 90–180° off the track, so the jet ends up flying sideways or backwards at that speed. It holds height through it on the bench.
- Coming back from that takes as long as the lever profile needs to reverse the speed, about 13 s from 45 kt backwards.
- `StovlPedalPirouette = false` gives 0.5.5's coordinated curve back.

## 2. STOVL hand-overs 10 kt later

`StovlRegimeShift` default 20 → 30. Your file held 20, so it is moved once; the log says so, and you can set it back.

| Hand-over | 0.5.5 | 0.5.6 |
|---|---|---|
| Lever: ground-speed command → speed command (ACCEL / DECEL) | 45–55 kt | 55–65 kt |
| Stick and pedal hover laws (and the pirouette) | 50–60 kt | 60–70 kt |
| Released-stick side-velocity hold | 35–50 kt | 45–60 kt |
| CTOL blend (CTOL control, lever = thrust) | 60–80 kt airspeed | 70–90 kt airspeed |

Bench, full lever from the hover:

| | 0.5.5 | 0.5.6 |
|---|---|---|
| CTOL blend starts at | 61 kt | 71 kt |
| 60 / 100 kt reached | 6.68 / 11.02 s | 6.98 / 10.85 s |
| Height band to 130 kt | −0.03 / +0.41 m | −0.15 / +0.30 m |

- **New: a 15 cm sag at 95–110 kt,** with the CTOL laws flying after the later blend. Slow (−0.05 m/s at worst), but new.
- **Full-throttle short take-off:** the jet now stays at 8–10 m until about 100 kt, then climbs. In 0.5.5 it was at 38 m by 92 kt. Lowest point after lift-off: 1.84 m in both.
- **Unchanged on the bench:** hover → 45 kt → stop, vertical landings, the hover pedal set, VTO, the mission case, and the deceleration from 380 km/h.

## Checks

- With `StovlPedalPirouette = false` and `StovlRegimeShift = 20`, the bench output is byte-identical to 0.5.5 on four cases (45 kt pedal, hover → 45 kt → stop, full lever, hover pedal).
- Offline smoke test under Mono: all type initializers OK.
- The source zip rebuilds the DLL byte for byte.
- New FCS-log columns `stPir` (pirouette weight) and `stPirLim` (its sideslip limit, deg; 0 = none), after `stVaneNow`.
- Bench scripts: `py.sh` / `pym.py`.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.6 and `meta.json`; 0.5.5 kept as `Vortex_FS20_FCS.dll.0.5.5.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.6-release`.
- Restart the game to load it.
