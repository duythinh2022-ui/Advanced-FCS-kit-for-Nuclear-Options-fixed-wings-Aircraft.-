# Revoker FCS 0.8.0: the Raptor's steady roll-rate cap

Base: 0.7.0. Minor bump: this is a law change.

**Status:**
- Compiled.
- Benched (full suite, plus closed-loop replays of the 01:23 log).
- **Not flown.**
- 0.7.0 is kept as `.dll.0.7.0.bak`.

## Tom (5 Nov, 01:21 / 01:26)
- "I thought the gov is airframe agnostic outside of the model."
- "revoker is also worse, sideslip is more now for low to mid aoa roll and reversals."

## What the 01:23 log shows (0.7.0, margin 6)
- **Reversals at 10–28° AoA:** 5.6–6.6° (0.6.0's 00:44 log: 7.3–9.2°), except one at **54.1 s: 14.5°**.
  - That one is a 200 deg/s reversal at 167 m/s with the pull coming in, 8 → 27° AoA.
  - The rate cap (`RevSim`) stayed at its 300 deg/s ceiling the whole time. Its forward model predicted 1.3–5° of sideslip.
- **Rolling at 3–35° AoA:** sideslip 90th percentile 6.6° (00:44: 6.0°). This flight rolled faster at lower AoA: velocity roll 90th percentile
  199 vs 172 deg/s, median AoA 12 vs 20°.

## The change
The steady-rate cap is now the **F-22E 3.0.0 reversal model, the Raptor's own**. `RevSim` (the stock jets' 1 Nov forward model) is off. With
that, the governor is the Raptor's apart from:
- the per-jet settings: margin 6, `RollPaceRateBlend` 0.4 (the Raptor's 0.41);
- the estimator;
- the 0.7.0 rudder reach pass.

## Bench (0.7.0 → 0.8.0)
| | 0.7.0 | 0.8.0 |
|---|---|---|
| Sweep, stop β worst / mean | 5.4 / 3.01° | **3.9 / 2.47°** |
| Sweep, reversal β worst / mean | 8.5 / 3.78° | **6.2 / 3.41°** |
| Sweep, mean roll | 113 deg/s | **106 deg/s** |

- **Regression suite:** every case equal or 0.3–3.0° less (e.g. pull-then-roll 400 5.5 → 2.9°; push+roll 200 2.5 → 1.0°; corkscrew −55 1.9 → 0.6°).
  Peak roll rates are down in several cases (fullaft+roll+rev 500: 145 → 109 deg/s).
- **Corkscrews:** −0.7…−3.3° in 19 of 20 cases (pull1.0 + roll 550: 9.0 → 5.8°). One is +0.6° (pull0.8 + roll 250: 1.3 → 1.9°). Velocity roll is
  10–50 deg/s lower.
- **Damage suite:** better or equal everywhere. One time cost: a clean leaf stop at 370 km/h takes 1.98 s instead of 1.73 s.
- **Replays of the 01:23 log:**

  | event | 0.7.0 | 0.8.0 |
  |---|---|---|
  | 54.1 s reversal | 11.1° (the game made 14.5°) | **6.7°** |
  | 52.4 s reversal | 7.9° | 6.5° (peak roll 199 → 167 deg/s) |

The price is roll rate at low to mid AoA, which you allowed for.

## Install
- `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 `f49e7a97415c3edf73638d91db556cbb`) and `meta.json`.
- Release: `C:\Games\NO mods\Revoker-FCS-0.8.0-release`.
