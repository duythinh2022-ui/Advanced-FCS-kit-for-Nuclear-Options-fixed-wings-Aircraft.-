# Ifrit FCS 0.13.1: `RollPaceRateBlend` default 0.65 → 0.4

Base: 0.13.0. Patch bump: a default setting change, no law change.

**Status:**
- Compiled.
- Benched (regression and sweep sets).
- **Not flown.**
- 0.13.0 is kept as `.dll.0.13.0.bak`.

## Tom (5 Nov, 10:16)
"set the default blend settings to ifrit as 40"

(Background: the 09:56 log was flown at 0.65 throughout. The blend edits during that flight went into the Revoker's config, which now holds 0.)

## The change
- `6. Lateral governor / RollPaceRateBlend` defaults to **0.4**.
- A config file still holding exactly 0.65 is moved to 0.4 once (`GovernorRevision` 5). A value you set by hand is kept. Your file holds 0.65,
  so it moves.
- The setting takes effect at once.

## What it does (09:56 log, full-stick rolls at 10–35° AoA)
- The stick asked for about 101 deg/s, and the rate cap sat at about 34.
- The roll target is the stick demand − blend × (stick demand − cap):
  - at 0.65 it came out at about 56 deg/s;
  - at 0.4 it would be about 74 deg/s, before the yaw-pacing window trims it.

## Bench (blend 0.65 → 0.4, margin 6)
**Stop/reversal sweep:**

| | 0.65 | 0.4 |
|---|---|---|
| Mean roll | 108 deg/s | **115 deg/s** |
| Stop sideslip, mean / worst | 3.14 / 4.6° | 3.83 / 6.2° |
| Reversal sideslip, mean / worst | 4.76 / 7.7° | 4.91 / 8.5° |

**Regression suite:** mean roll up 10–30% in most rolling cases; sideslip mostly +0.2–2.5°.
- Worst: push-then-pullthrough + roll 350, 2.6 → 7.8°; roll-then-pull reversal 400, 7.0 → 9.5°; fullaft + roll + reversal 500, 2.8 → 4.7°.
- Better: pull-then-pushover + roll 350, 8.6 → 6.6°.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `a41101998dcac380e554d1360e9833fc`) and the `meta.json` beside it.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.13.1-release`.
