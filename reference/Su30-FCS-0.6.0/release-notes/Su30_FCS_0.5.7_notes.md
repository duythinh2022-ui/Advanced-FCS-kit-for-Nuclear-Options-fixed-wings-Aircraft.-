# Su-30VXN FCS 0.5.7: max pitch rate 60 deg/s; a roll reversal stops the old rotation first, past the sideslip margin

Base: 0.5.6. Tom, 12 Nov:
- 13:42: "increase maximum default pitch rate of the flanker to 60deg/s"
- 13:52: "look at the latest log, at the end it didnt stop the yaw even tho i tried to reversed it, the rule for the flanker is, stop the roll or yaw even if that requires the sideslip is outside the margin"

**Changelog:**
- **Max pitch rate 48 → 60 deg/s** (dev `MaxPitchRate`, the F-22E's value). A dev config still holding 48 is moved to 60 once. A value you set by hand is kept.
- **Stopping the old rotation in a reversal.** Moving the stick to the other side now gets the same treatment as releasing it: once the sideslip is past the margin, the jet stops the rotation it is in at full rate instead of holding the roll on the old side to protect the sideslip. Then it reverses. New dev-only static `StopHardOnReversal` (on).

**Status:**
- Compiled. Built twice, same bytes: 1,524,736 bytes, SHA-1 e0d4a9a03c14e67ca0dce8a217b351d784c4322d. The 0.5.6 source rebuilt here gives the released 0.5.6 DLL byte for byte.
- Native core unchanged (`c161ace4…`).
- Benched: full suites, targeted pitch cases, and a closed-loop replay of your 13:34 log.
- Run on the game's Mono under Wine: native self-test 200 / 200 PASS; gear patch PASS; flap path JIT PASS.
- **Not flown.**

## 1. Max pitch rate 60
`MaxPitchRate` is the ceiling of the pitch-rate budget everywhere:
- the rate command in MPO and the gear-down law;
- the AoA / G law's rate part;
- the push side.

**What 0.5.6 already did at 60:** full aft stick already reached 60, through `FullStickPitchRate`. That setting ramps the cap from MaxPitchRate to 60 over the last 20 % of aft stick, in the AoA / G law only. It is now flat, since both are 60.

**What changes, then:**
- every stick position short of full aft;
- every push;
- MPO and the gear-down law.

The rate per unit of stick goes up by up to 25 % wherever the budget reaches the cap.

**Bench, 1,500 m, full thrust, 4 s** (48 → 60):

| Case | Peak pitch rate (deg/s) | Peak AoA | Peak g |
|---|---|---|---|
| Full aft, 300 / 450 km/h | 62.4 / 59.9, unchanged | 75 | same |
| 0.4 stick, 300 km/h | 14.2 → 17.7 | 13.8 → 18.7° | 3.60 → 3.87 |
| 0.4 stick, 450 km/h | 13.9 → 17.4 | 9.1 → 10.9° | 4.66 → 5.33 |
| **0.7 stick, 300 km/h** | 34.6 → 41.6 | **61.4 → 74.7°** | — |
| **0.7 stick, 450 km/h** | 29.0 → 36.6 | **29.6 → 64.4°** | — |
| 0.5 stick then release, 450 km/h | 18.6 → 23.2 | 11.5 → 14.3° | 5.34 → 6.10 |
| 600 / 800 km/h, any stick | — | — | same (g-limited) |
| Full aft then full forward, 300 / 500 km/h: push rate | −49.7 / −50.1 → −63.2 / −62.1 | — | — |
| Full aft + full roll, 600 km/h: rate overshoot | 62.6 → 67.3 | — | — |

- **The 0.7-stick rows change the stick feel.** At 300–450 km/h, 0.7 stick now asks for more pitch rate than the flight path turns at, so the nose keeps coming past where it used to settle.
- **Pitch curves:** no new reversals or hitches in the onset (pitch-rate sign changes in the first 1.5 s: 0 / 0). The full-aft + roll case had 2 reversals at 60 against 4 at 48.

**Suites** (48 → 60):
- **Corkscrews (20):**
  - mean peak sideslip 9.67 → 10.87°, worst 22.0 → 19.8°;
  - mean roll rate 52 → 43 deg/s (the partial-stick cases pull to a higher AoA);
  - roll smoothness mean 25.5 → 26.0, worst 45 → 63.
- **Regression set (20):** mean peak sideslip 7.96 → 8.28°, worst 12.7 → 13.6°.
- **Sweep (32):**
  - stops: mean sideslip 5.31 → 5.81°;
  - reversals: 7.61 → 7.53°;
  - reversal time 0.36 s both.

## 2. Reversal = stop first (`StopHardOnReversal`)
### Your 13:34 log, 838–844 s
| Time | What happened |
|---|---|
| 838.2 s | Falling leaf at 74° AoA, yaw −52 deg/s, stick released |
| 839.0–839.3 s | The hard stop (0.2.0, "stop even if the sideslip is past the margin") ran for 0.3 s |
| 839.4 s | You put in full right stick, and the hard stop dropped, because it only counted a released stick |

**What the law then did, 839.4–841.3 s:**
- The pace window held the roll target on the yaw: commanded roll −15 → −25 deg/s, to the left, with full right stick.
- The sideslip hold rolled the jet into the sideslip.
- The nozzle differential went back to the left-yaw side (+4 to +14°).
- The yaw stayed at −45 to −49 deg/s for 2.9 s while the sideslip grew 16 → 31°.

It only came down once the soft AoA limit handed the jet to MPO at 841.3 s (yaw −2 at 843.2 s).

### Change
Lateral stick against the velocity roll in progress, with the pedal released or against the yaw, now counts as a release for the hard stop.
- **The stick's own reversal target stays.** What stands aside is what held the roll on the old side: the pace window, and the sideslip hold.
- **The sideslip hold steps aside in proportion** to how far the sideslip is past the margin, all of it at twice the margin. Without that proportioning, a low-AoA reversal just past the margin was slowed (bench, 400 km/h, 11° AoA: 0.32 → 0.53 s). With it: 0.37 s.
- **It ends** as soon as the velocity roll is on the stick's side, or is down to 5 deg/s.

### Replay of 836–846 s (closed loop, your sticks; free replay reproduces the game: 0.5.6 yaw through −2 deg/s at 843.3 s, game 843.2)
| | 0.5.6 | 0.5.7 |
|---|---|---|
| Yaw stopped (through −2 deg/s) | 843.3 s | **840.9 s** (2.4 s sooner; 1.5 s after the stick went right) |
| Nozzle differential in the stop | +4 … +14° (wrong side) | −4 … −15° (braking side; full −15 from 840.2 s) |
| Peak sideslip | 30.1° | 36.6° |
| Yaw at 844 s, reversing right | +20 deg/s | +58 deg/s |

- The larger sideslip is the trade your rule allows.
- The hard stop ends at 840.75 s, when the velocity roll is on the stick's side. The sideslip hold then comes back with the sideslip at 33–36°. It rolls the jet left, up to 8 deg/s, for about 1 s, while the yaw keeps accelerating right.

### Suites (60 deg/s, reversal fix off → on)
- **Falling-leaf reversals (damage suite, clean jet, 296 / 370 km/h):** never reversed in the window → reverse in 2.40 / 2.50 s; peak sideslip 16.1 / 15.7 → 18.7 / 18.2°.
- **Regression set, roll-then-pull reversal at 400 km/h:** sideslip 11.8° both; mean roll 42 → 44 deg/s.
- **Sweep:** one case changed, 400 km/h, 0.4 stick reversal: 0.32 → 0.37 s, sideslip 10.7° both. The other 31 are identical.
- **Corkscrews:** identical.

## Files
- `Plugin.cs`: `MaxPitchRate` default 60, `Su30DevRevision` 15 with the one-time 48 → 60 move, `FullStickPitchRate` description.
- `FcsController.cs`: `StopHardOnReversal`, `StopHardRevKeepsSlipHold` (off), `stopHardIsRev`, `stopHardRevSlipW` in the hard stop. The stop's zero target is skipped in a reversal, and the sideslip-hold removal is proportional in a reversal.
- `FcsStandalonePlugin.cs`: 0.5.7.

Previous notes: `claude/Su30_FCS_0.5.6_notes.md`.
