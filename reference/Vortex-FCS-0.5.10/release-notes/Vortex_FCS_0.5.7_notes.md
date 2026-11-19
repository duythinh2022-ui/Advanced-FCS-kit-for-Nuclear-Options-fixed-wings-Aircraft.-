# Vortex FCS 0.5.7: Stability Assist off no longer rolls past what it can stop; the pedal turn corrects the skid again

18 Nov 2026. On 0.5.6.

**Status: benched, not flown.**

Tom, 18 Nov:

> no i want that to happen, since i dont command side skid so the jet must correct for it. And check latest log, the jet just flipped an crashed

## 1. The flip (your 11:51 log)

**What happened** (`Vortex_FCS_20261118_115108.csv`):

| Time | What the log shows |
|---|---|
| 28–36 s | Stability Assist on, the 0.5.6 pirouette: nose round at 20–27 deg/s, then back to the hover. |
| 40.25 s | Stability Assist off at 2 m/s. The 0.5.4 throttle hold worked: lever set to 65.9 % (was 54.7 %). |
| 41.2–44.7 s | Full right pedal: yaw 20–22 deg/s. |
| 43.0–44.6 s | Full right stick. The freestyle law commanded **90 deg/s of roll**. The roll posts sat at their stop the whole time; the roll reached 69 deg/s by 62° of bank. |
| 44.7–45.2 s | Stick released, then full left. The posts at the opposite stop could only brake at ~50 deg/s², and the bank ran on to 103°. |
| 45.3 s | Ground contact, then the flip. |

**Why.** Since 0.2.2, Stability Assist off (freestyle) has had a fixed 90 deg/s roll rate and no bank limit. 90 deg/s is more than the hover's roll axis can stop: from 69 deg/s it needs ~50° more bank to stop. Past ~60° the lift system's thrust goes sideways. This breaks your rule that the FCS doesn't let the pilot command what the jet can't hold. It only showed now because before 0.5.4 the engine dropped to idle in freestyle, so nobody flew it for long.

**Fix (freestyle only):**
- **Roll rate:** full stick is the rate the roll axis can start and stop within `StovlYawStopTime` (1.25 s) at its stopping power. That is the pedal's rule since 0.3.9. `StovlRollRateMax` (90) is now only the upper bound.
- **Bank limit:** while the lift system carries the jet, the bank is held to `StovlBankMax` (45°, the STOVL laws' own limit). It is approached at a rate that can stop on it. The limit opens to none as the wing takes the weight (the same airspeed blend as the velocity-vector roll), so freestyle at speed rolls as before.

Bench, your jet, hovering at 16 m with Stability Assist off:

| Case | 0.5.6 | 0.5.7 |
|---|---|---|
| The log's inputs (pedal, full right stick 1.5 s, full left): peak bank / roll rate | 48.1° / 41.8 deg/s | 40.5° / 29.5 deg/s |
| Half right stick 2 s, then released | 82.9° and still rolling at ground contact | 28.8°, held |

The bench roll is slower than the game's (41.8 against 69 deg/s at full stick), and in both builds the bench jet reached the ground before inverting. So the bench shows the limit working, not a replay of the flip.

**What stays yours: height.** The throttle in Stability Assist off is yours, held at the hover value on entry (0.5.4). A bank tilts the lift: at 28°, with the throttle untouched, the jet was sinking at 5.3 m/s and 10 m lower 5 s after the stick, and reached the ground from 16 m. Bank needs throttle with it, as in any manual hover.

## 2. Pedal turn: the skid is corrected again

- **`StovlPedalPirouette` is now off by default.** Your config still holds 0.5.6's `true`, so it is moved once (log: `StovlPedalPirouette true -> false`).
- The pedal turn is the coordinated one again: the FCS removes the skid while the nose turns.
- **Why the yaw can't be faster.** The yaw rate is capped at what the sideways acceleration available can bring the flight path round with. With the skid corrected, that cap is the "max available yaw rate". The full yaw-axis rate (~23 deg/s) at 30–45 kt would need 6–9 m/s² sideways; your jet has about 4.7.
- **More of that budget for the turn.** `StovlTurnShare` 0.707 → 0.85. It's in the dev file; yours held 0.707, so it is moved once.

Bench, 13.8 t, full pedal 8 s, stick released:

| `StovlTurnShare` | 0.707 (0.4.7–0.5.6) | **0.85 (0.5.7)** | 1.0 |
|---|---|---|---|
| 30 kt: yaw rate | 11.3 deg/s | **12.9** | 13.0 |
| 30 kt: largest skid / sideslip | 0.75 m/s / 1.7° | **1.25 / 3.2** | 1.78 / 5.0 |
| 45 kt: yaw rate | 7.2 deg/s | **8.4** | 9.7 |
| 45 kt: largest skid | 0.52 m/s | **0.82** | 1.43 |
| Bank the other way after release (30 / 45 kt) | 3.6 / 2.9° | **3.6 / 3.1°** | 3.6 / 10.1° |

Height ±4 cm in all. Share 1.0 holds more skid through the turn and needs 10° of opposite bank to take it out after a 45 kt turn, so 0.85 is the default.

## Checks

- Regression set (vertical landings, hover pedal set, VTO, short take-off, mission) within 0.04 s / 0.01 m of 0.5.6.
- With `StovlTurnShare = 0.707`, the Stability Assist on bench output is byte-identical to 0.5.6 with the pirouette off. A freestyle hover with no stick input is unchanged.
- Offline smoke test under Mono: all type initializers OK. The source zip rebuilds the DLL byte for byte.
- New FCS-log columns `stFreeRollMax` and `stFreeBankLim` (freestyle's roll rate cap and bank limit), after `stPirLim`. Your next Stability Assist off flight will show the game's actual cap.
- Bench scripts: `fs.sh`, `skm.py`.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.7 and `meta.json`; 0.5.6 kept as `Vortex_FS20_FCS.dll.0.5.6.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.7-release`.
- Restart the game to load it.
