# Ifrit FCS 0.11.0: the roll stops when you let go, the plan stops the roll through a push, and a 6° stop margin

Base: 0.10.0. Minor bump: these are law changes.

**Status:**
- Compiled.
- Benched (full suite, plus closed-loop replays of the 00:23, 00:52 and 00:59 logs).
- **Not flown.**
- 0.10.0 is kept as `.dll.0.10.0.bak`.

(Correction to the 0.10.0 notes: 0.9.1 was installed at 00:35, so the 00:23 log was flown on **0.9.0**.)

## Tom (5 Nov, 00:56 / 01:02 / 01:10)
- The roll kept going after release, "much longer" than a split second.
- Roll reversals at low to mid AoA make 7–9° of sideslip: "reduce by 2 degs, even if sacrificing some roll rate".
- A negative-AoA corkscrew held on after the stick went to a positive one.
- Decisions (01:10):
  - "do slow down the pitch and stop the roll during that pitchdown/aoa transient";
  - "do RollStopSideslip for both".

## 1. Roll after release (`PushGovRelA` 20°)
**What happened:**

| log | time | AoA | roll at release → peak | time to stop |
|---|---|---|---|---|
| 00:52 | 69.9 s | −17° | 57 → 87 deg/s | 1.0 s |
| 00:23 | 125 / 131 / 183 / 262 s | | e.g. 84 → 110, 22 → 52 deg/s | 1.1–1.9 s |

**Cause:**
- The Raptor push governor's lateral band was holding the roll target, or growing it, while the AoA headed back toward zero.
- Its limit, L = (pS cos α · AoA-rate − capacity) / sin α, grows as 1/sin α near zero AoA.

**Fix:** with the lateral stick released below 20° AoA, the law's own stop runs. Above 20° the band stays. Switching it off there cost 5–7° in the
damaged-tail leaf stops at 45–50°.

**Bench replays:**

| case | stop time | sideslip |
|---|---|---|
| 00:52, 70 s | 1.40 → 0.43 s | 8.2 → 7.5° |
| 00:23, 183 s | 1.28 → 0.65 s | 5.2 → 3.1° |
| 00:23, 262 s | 1.77 → 0.70 s | 6.1 → 6.1° |

On its own, this change left the regression, sweep and corkscrew suites identical.

## 2. The push plan stops the roll (`PushPlanStopsRoll`)
**The change:** under a planned push or pull-through, the velocity roll bleeds to zero over the plan's time with the stick **held** too, not only
released. The roll comes back to your stick when the plan ends, below 10° AoA. The pitch is still paced by the plan.

**Why it helps:** a held roll keeps making the yaw that coordinates it, and the push has to shed that yaw before zero AoA.

**Replays of your log (0.10.0 → 0.11.0):**

| event | 0.10.0 | 0.11.0 |
|---|---|---|
| 00:59 corkscrew switch, 73 s | 2.48 s to 10° AoA, β 5.1° | **1.90 s, β 2.8°** |
| 00:23, 232 s | β 5.2° | 4.2° |
| 00:23, 257 s | β 9.7° | 8.5° |
| 00:23, 214 s (held push) | β 6.0° | 2.9° |
| 00:23, 107 s | β 8.8° | 8.2° |
| 00:23, 239 s | β 4.0° | **7.5°** (worse) |

At 239 s the roll stick was re-applied mid-push. It now waits for the plan to end, and the sideslip builds as the roll resumes after the bottom.

**Damage suite (clean held pushes):** 10° AoA in **1.50 s instead of 1.82 s**, sideslip 9.5 / 10.8 → 8.0 / 6.4°. Every changed damage case is
better.

## 3. `RollStopSideslip` 8 → 6
**What the flights show:**
- 00:59 log: 5 reversals at 8–21° AoA peaked at 7.9–15.5°.
- 00:23 log: 5 at about 22° peaked at 8.2–10.5°.
- In nearly all of them the 8° margin was used up.

**The change:**
- The default is now 6.
- A config file still holding exactly 8 is moved to 6 once (`GovernorRevision` 3). A value you set by hand is kept.
- Expect about 2° less in reversals where the margin is spent, at some roll rate.

**Not fixed by this:** the worst ones at 160–200 m/s (11.5–15.5°). There the roll swings ~700 deg/s², the yaw follows at 65–90, and nothing caps
the steady rate (`RollPaceRateBlend` 0). That needs a rate cap fed a yaw reading corrected at high IAS (the estimator reads 1.5–2× there). Open.

**Bench, full suite (0.10.0 → 0.11.0, margin 6):**
- **Sweep:** stop β mean / worst 4.96 / 9.7 → 4.75 / 8.0°; reversal 5.57 / 9.7 → 5.35 / 9.6°; mean roll 120 → 119.
- **Regression suite:** mostly −0.3…−1.4°.
- **Worse:**
  - pull-then-pushover + roll 350: 12.1 → 12.9°;
  - sweep 400 / 0.70 stop: 6.0 → 7.4°, but it stops in 0.70 s instead of 1.23 s;
  - sweep 500 / 0.55 reversal: 9.0 → 9.6°.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` and the `meta.json` beside it.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.11.0-release`.
