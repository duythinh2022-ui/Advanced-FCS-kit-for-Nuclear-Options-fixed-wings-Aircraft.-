# Revoker FCS 0.8.1: low to mid AoA roll set between 0.5.1 and 0.8.0 (`RollPaceRateBlend` 0.4 → 0.65)

Base: 0.8.0. Patch bump: a default change, no law change.

**Status:**
- Compiled.
- Benched (full suite, plus closed-loop replays of 38 reversals).
- **Not flown.**
- 0.8.0 is kept as `.dll.0.8.0.bak`.

## Tom (5 Nov, 01:40)
"i want somewhere in between 0.5.1 and 0.8 for low to mid aoa roll"

## The change
- `6. Lateral governor / RollPaceRateBlend`: the default goes **0.4 → 0.65**. This is the share of the roll target that the steady-rate cap
  sets; since 0.8.0 that cap is the Raptor's.
- A config file still holding exactly 0.4 is moved once (`GovernorRevision` 3). A value set by hand is kept.
- It takes effect at once, so you can try other values in game without a new build.

## Replays: the 38 reversals at 10–28° AoA from the 00:44 and 01:23 logs
| | 0.5.1 (pre-port) | 0.8.0 (blend 0.4) | blend 0.6 | **0.8.1 (blend 0.65)** | blend 0.7 | blend 0.8 |
|---|---|---|---|---|---|---|
| Mean peak sideslip | 3.74° | 4.79° | 4.46° | **~4.3°** (interpolated) | 4.16° | 4.06° |
| Mean peak velocity roll | 82 deg/s | 103 deg/s | 91 deg/s | **~88 deg/s** | 85 deg/s | 80 deg/s |
| Mean time to zero roll | 0.40 s | 0.52 s | 0.44 s | | 0.42 s | 0.37 s |

The midpoint between 0.5.1 and 0.8.0 is 4.27° and 92 deg/s.

## Suite (0.5.1 / 0.8.0 / 0.8.1)
- **Sweep, stop β mean:** 2.24 / 2.47 / **2.29°**.
- **Sweep, reversal β mean:** 2.71 / 3.41 / **3.07°**. The worst case is 5.6° (0.5.1 5.2°, 0.8.0 6.2°).
- **Sweep, mean roll:** 100 / 106 / **100 deg/s**.
- **Regression suite:** mostly between the two (pull-then-roll 400: 1.9 / 2.9 / 1.8°; fullaft+roll+rev 500: 2.7 / 4.5 / 3.5°). Some steady roll
  rates fall below 0.5.1's (pull-then-roll 250: mean 20 / 22 / 17 deg/s).
- **One case is worse than both:** fullaft+roll 250, 1.6 / 1.9 / **2.8°**.
- **Damage suite against 0.8.0:**
  - clean held push-overs 2.1 → 3.3° and 2.3 → 4.2°;
  - clean released push-overs 3.0 → 2.2° and 3.2 → 2.4°;
  - the clean leaf reversal at 370 km/h 1.6 → 2.4°, but faster (1.38 → 1.10 s).

## Install
- `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 `0624964ff4a7c28d99c5a6320f604fbb`) and `meta.json`.
- Release: `C:\Games\NO mods\Revoker-FCS-0.8.1-release`.
