# Ifrit FCS 0.15.0: the push governor's lateral band is removed, and so are both gates

Base: **0.13.1**. 0.14.0's gate is not carried. Minor bump: this is a law change.

**Status:**
- Compiled.
- Benched (full suite, plus closed-loop replays of 16 events).
- **Not flown.**
- 0.14.0 is kept as `.dll.0.14.0.bak`.

## Tom (5 Nov, 14:31 / 14:34)
- On 0.14.0: "is it another gate or ontop fix?" Yes, it was a gate.
- "i dont wanna add any more gates or on top logic, like if i release any axis the jet must try to stop that axis"

## What was removed
**The push governor's lateral band** (from the Raptor's 2.34, ported in 0.9.0).
- While the AoA swept toward zero, it bounded the velocity roll's rate of change below by L = (pS cos α · AoA-rate − capY) / sin α.
- When L ≥ 0 it held or grew the roll the pilot was stopping or reversing:
  - a released roll kept going (00:52 log 69.9 s; 00:23 log 125–262 s);
  - a 41 deg/s right roll was held for 2.9 s against full left stick, AoA 41 → 11° (14:10 log, 86.9 s).
- Its yaw read there was 3–5 deg/s². The yaw delivered 16–24 deg/s² once the roll was let go.

**The two gates that only switched it off in certain cases:** 0.11.0's `PushGovRelA` (released stick below 20° AoA) and 0.14.0's
`PushGovRevYield`.

**What stays:**
- A stop or reversal is the roll law's own, paced by the pace window and the rate cap on the measured yaw.
- The push governor keeps its pitch side: the AoA sweep toward zero slowed to what the yaw sheds. It slows only, and never turns the nose back.
- Planned push-overs keep the plan's roll bleed (0.11.0, which you asked for).

## Bench: replays (0.13.1 → 0.15.0)

| Event | Time to stop / reverse | Peak sideslip |
|---|---|---|
| 14:10, 86.8 s (reversal held against) | 1.90 → **1.57 s** | 2.8 → 4.1° |
| 00:52, 69.9 s (released) | 0.42 → **0.27 s** | 7.1 → 6.2° |
| 00:23, 125 s (released) | 0.88 → **0.72 s** | 5.1 → **8.0°** |
| 00:23, 131 s (released) | 1.17 → **0.90 s** | 3.6 → 2.9° |
| 00:23, 183 s (released) | 0.95 → **0.78 s** | 3.4 → 3.5° |
| 00:23, 262 s (released) | 1.78 → **0.93 s** | 4.2 → 2.0° |
| 01:19, 43.8 s (reversal) | 0.68 → 0.68 s | 3.4 → 4.5° |
| 01:19, 52.5 s (reversal) | 0.95 → 1.00 s | 4.0 → **6.3°** |
| 00:23, 104.8 s (reversal) | 0.43 → 0.40 s | 9.6 → 10.2° |

The 7 push-overs replayed (09:16 log, 82.3 / 88.5 s; 09:56 log, 31.9 / 59.6 / 64.8 s; 14:10 log, 69.0 / 78.6 s) are within ±0.9° and
±0.07 s.

## Bench: full suite
- **Stop/reversal sweep and corkscrews:** identical.
- **Regression suite:**
  - better: push + roll 200, 4.1 → 2.8°; pull-then-pushover + roll 350, 6.6 → 6.1°; push-then-pullthrough 450, 9.5 → 8.9°; pullthrough with the roll
    released, 5.9 → 5.4°;
  - worse: push-then-pullthrough + roll 350, 9.1 → 10.7°; roll-then-push −55, AoA overshoot past zero +5.9 → +9.0°.
- **Damage suite: the cost of the change, all with damaged tails.** The band was holding the roll there to spare the yaw.
  - One ruddervator, leaf stop 370: 2.8 → **7.6°** (1.80 → 2.12 s).
  - One ruddervator, leaf reversal 370: 3.8 → **6.9°**.
  - One ruddervator, leaf reversal 296: sideslip after the event 3.4 → 5.0°.
  - Both ruddervators, held push 560 / 600: sideslip after 5.7 → 8.7° and 4.6 → 9.6°.
  - The clean jet is unchanged within 0.5°.
  - The 5 HP rolling pull at 900 km/h departs in both builds (the tail breaks off).

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `2739f5e9407fe1b424a3605c0cf562a3`) and the `meta.json` beside it.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.15.0-release`, with the main zip, Optional DevUnlock, Source (with the bench) and these notes.
