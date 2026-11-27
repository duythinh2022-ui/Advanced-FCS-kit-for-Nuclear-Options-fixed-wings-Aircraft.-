# Ifrit FCS 0.10.0: the push plan slows down when a held roll keeps adding yaw

Base: **0.9.1** (the other chat's engine-down creep fix, installed at 00:15, so the 00:23 log was flown on 0.9.1). Minor bump: this is a law change.

**Status:**
- Compiled.
- Benched, plus closed-loop bench replays of the 5 Nov 00:23 log.
- **Not flown.**
- 0.9.1 is kept as `.dll.0.9.1.bak`.

## Tom (5 Nov, 00:33, on the 00:23 log)
"quick snap roll aos is too high i want it to be on avg 3degs lowered, most pushover held and release are very good, good aos. But there are
some occasion that aos exceeded 17 and 20degs"

## The two big push-over excursions
Both are pushes through zero AoA at low speed, with the lateral stick held full the whole way:

| when | what | peak β |
|---|---|---|
| 232.3 s | full aft + full roll, from −59° AoA, 56 m/s | **17.6°** |
| 256.4 s | full forward + full roll, from +61° AoA, 67 m/s | **24.6°** |

**Cause.**
- The push plan sets its pitch rate once, from the yaw rate at the start of the push: 24 deg/s at 256.4 s. Then it is rise-only: it can speed
  up, never slow down.
- With the roll stick still held, the roll kept growing during the push. That added the yaw that coordinates it: 41 deg/s by 257.0 s.
- The plan stayed at 48 deg/s. About 29 deg/s of yaw was still on at zero AoA, where yaw turns straight into sideslip, against the 17 deg/s that
  the 8° allowance covers.

**The change (`PushPlanFall`).**
- The plan's pitch rate may now also come **down** to what the yaw actually on board allows. This uses the plan's own model:
  T = (r − rLeft) / capPlan, and q = (AoA − 10°) / T.
- It applies only above 30° AoA. Below that, the formula runs to zero near the bottom whatever the yaw is, and a drop there would be a bump.
- It only ever slows the push. The command is never positive with the stick forward.

**Bench:**
- **Full suite, against 0.9.1:** the regression, sweep and corkscrew suites are **identical**. The damage suite only gets better:

  | case | 0.9.1 | 0.10.0 |
  |---|---|---|
  | Clean held push, 560 km/h | β 26.6° | β 9.5° |
  | Clean held push, 600 km/h | β 28.7° | β 10.8° |
  | One ruddervator, released / held | β 16.8 / 19.2° | β 13.1 / 15.5° |

  The cost: those clean held pushes (full roll held) reach 10° AoA in **1.82–1.83 s instead of 1.50–1.52 s**. Released pushes are unchanged
  (1.35 / 1.33 s). The same change on 0.9.0 gave the same picture.
- **Replays of your log** (closed loop; the bench under-reads these events, game 17.6 / 24.6):

  | event | 0.9.1 | 0.10.0 |
  |---|---|---|
  | 232 s | 11.0° | **5.2°** |
  | 256 s | 13.8° | **9.7°** |

  Your 9 other push-overs in the log replay **identically** (β and time to 10° AoA).

## Snap rolls: not changed in this build; the knob is `RollStopSideslip`
**What the log shows.**
- Rolling pulls and reversals at 10–35° AoA peak at **8.4° mean**. 0.8.2's (22:57 log) peaked at 3.9°.
- In 22 of your 64 roll events the pace window had **used up the whole 8° margin** (margin left ≤ 1.5°). Those peaked at 10.0° on average. The
  other 42 averaged 4.2°.
- So the window is spending the margin you set (`6. Lateral governor / RollStopSideslip = 8` in your config). Then it overshoots about 2° while
  the yaw catches up.
- 0.8.x's models were conservative and never reached 8. The 3.0.0 estimator reads the capacity correctly (0.93–1.10 of what was delivered in
  this log), so it now does reach it.

**Suggestion: set `RollStopSideslip` = 5.** It takes effect at once, with no new build.
- **Expected in flight:** the margin-bound events drop by about 3°, to roughly 7°, with a somewhat slower roll in those events.
- **The bench can't confirm this.**
  - It doesn't reproduce the margin binding: replays of 14 of those events give 6.8° at 8 and 6.5° at 5. The game gave 9.0°.
  - Suite at 5: sweep stop/reversal mean −0.5 / −0.3°, corkscrews mostly −0.4…−2.7°.
  - But one case is worse: pull-then-pushover + roll at 350 km/h, 9.2 → 12.4°.

  That is why it isn't migrated into your file.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `ed9d90002f53a282e298894849e6779f`) and the `meta.json` beside it.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.10.0-release`.
