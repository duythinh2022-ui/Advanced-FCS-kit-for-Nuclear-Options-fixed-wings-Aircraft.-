# Ifrit FCS 0.12.0: the Raptor's steady roll-rate cap (`RollPaceRateBlend` 0 → 0.65)

Base: 0.11.0. Minor bump: this is a law change, the same one as Revoker 0.8.0 / 0.8.1.

**Status:**
- Compiled.
- Benched (full suite, plus closed-loop replays of the 01:53 log).
- **Not flown.**
- 0.11.0 is kept as `.dll.0.11.0.bak`.

## Tom (5 Nov, 01:54 / 02:01)
"towards the end, ifrit aos exceeded 10degs" → "do it" (the Raptor rate cap at blend 0.65, as on the Revoker).

## What the 01:53 log shows
- **What happened:** four back-to-back reversals at 56.7–59.8 s, 12–22° AoA, about 140 m/s, half to two-thirds pull.
- **Sideslip:** 7.7 / 9.8 / 10.2 / 10.6°, with the 6° margin used up each time.
- **Roll going in:** 85–126 deg/s.
- **Cause:** nothing capped the steady roll rate. `RollPaceRateBlend` was 0, and `RevSim` sat at its 300 deg/s ceiling.

## The change
- The steady-rate cap is the F-22E 3.0.0 reversal model, the Raptor's. `RevSim` is off.
- The default `RollPaceRateBlend` goes 0 → **0.65**. A config file still holding exactly 0 is moved once (`GovernorRevision` 4); a value set by hand
  is kept.
- The setting takes effect at once. Higher means less sideslip and slower rolls; 0.41 is the Raptor's.

## Bench (0.8.3 pre-port / 0.11.0 / 0.12.0)
| | 0.8.3 | 0.11.0 | 0.12.0 |
|---|---|---|---|
| Sweep, stop β mean | 2.77° | 4.75° | **3.14°** |
| Sweep, reversal β mean | 3.18° | 5.35° | **4.76°** |
| Sweep, mean roll | 101 deg/s | 119 deg/s | **108 deg/s** |
| Corkscrews, β mean / worst | 2.9 / 5.0° | 5.9 / 12.4° | **3.6 / 6.2°** |
| Corkscrews, velocity roll | 52 deg/s | 65 deg/s | **45 deg/s** |

- **Regression suite:** no case worse than 0.11.0, most 0.4–4° better (fullaft+roll+rev 500: 7.2 → 2.8°; push-then-pullthrough 350: 6.2 → 2.6°;
  pull-then-pushover 350: 12.9 → 8.6°).
- **Damage suite:**
  - one-tail cases 2–7° better (one-tail released / held push 560: 12.9 / 13.5 → 6.5°);
  - both-tail pushes +1.4–1.5° (7.7–7.9 → 9.1–9.4°);
  - clean held pushes +0.7 / +1.7° (8.0 → 8.7°, 6.4 → 8.1°).
- **Replays of the 01:53 reversals:** peak 7.6 → **4.7°** (the bench under-reads them; the game made 10.6°).

**The price:** corkscrew roll rate is about 30% below 0.11.0 and 13% below 0.8.3. Stop/reversal sweep roll stays above 0.8.3's.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `ba18387642bb47434a23bec8a3ede94b`) and the `meta.json` beside it.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.12.0-release`.
