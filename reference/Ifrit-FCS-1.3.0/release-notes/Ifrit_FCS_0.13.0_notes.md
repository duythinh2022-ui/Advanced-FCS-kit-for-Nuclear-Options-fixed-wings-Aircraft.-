# Ifrit FCS 0.13.0: faster push-overs (the plan catches up with the yaw, and its own shed rate from negative AoA)

Base: 0.12.0. Minor bump: this is a law change. No setting changed; `PushoverSideslip` stays 8.

**Status:**
- Compiled.
- Benched (full suite, plus closed-loop replays of 14 push-overs from the 09:16, 01:53, 00:59 and 00:23 logs).
- **Not flown.**
- 0.12.0 is kept as `.dll.0.12.0.bak`.

## Tom (5 Nov, 09:20, on the 09:16 log)
"for pushover, i did a bunch of them towards the end, from positive to negative it is a little slow, maybe 2 or 3 more aos would speed it up.
But from negative to positive it is very very slow, aos is way way too tight"

## What the 09:16 log shows
Six planned push-overs, all corkscrew transitions with full roll held:

| push | start | plan start → 10° AoA | peak sideslip |
|---|---|---|---|
| + → − | 82.3 / 94.0 / 104.2 s | 1.58 / 1.73 / 1.63 s | 6.8–8.0° (the 8° allowance used) |
| − → + | 88.5 / 98.7 / 109.3 s | 1.85 / 1.30 / 1.60 s | 2.5–4.9° (allowance mostly unused) |

**Cause 1: the plan crawled up (both sides).**
- The push plan sets a pitch rate at the start. When the yaw sheds faster than planned, the rate is allowed to rise, but only at 15 deg/s²
  (`PushPlanRise`, from the F-22E).
- In 5 of the 6 pushes, the plan's own need (what the yaw left on board allows) reached the full 60 deg/s from about 40–45° AoA on. The plan was
  still crawling up toward it:

  | push | plan when the need hit 60 | plan at the bottom |
  |---|---|---|
  | 82.3 s | 37 deg/s | 45 |
  | 94.0 s | 26 | 33 |
  | 104.2 s | 34 | 42 |
  | 88.5 s | 17 | 27 |
  | 109.3 s | 28 | 35 |

  (98.7 s started with little yaw, so the plan was at 58 deg/s early and was not the limit.)

**Cause 2: the plan's yaw-shed model is from positive push-overs only.**
- The shed fit (K0 9.6 + K 0.0144 × IAS^1.5) came from positive push-overs.
- From negative AoA the tails sit in clean air, and the yaw sheds much faster:

  | side | measured shed ÷ fit (0.11–0.12 builds) |
  |---|---|
  | negative (01:53 36.9 s; 09:16 88.5 / 98.7 / 109.3 s) | ×1.29–1.64, mean 1.48 (28–30 deg/s² against the fit's 17–22) |
  | positive | ×1.10–1.24 |

- So the plan paced the negative pushes for 8° of sideslip, and they made 2–5°.
- At 88.5 s the planned rate even dropped from 17 to 10.6 deg/s in the first 0.5 s: `PushPlanFall` saw the yaw still at 50 deg/s and believed the
  fit.

## The changes
1. **`PushPlanRise` 15 → 60 deg/s².**
   - When the yaw is ahead of the plan, the plan follows its own need within a fraction of a second instead of over about a second.
   - The command itself is still shaped by `PushPlanJerk` (120 deg/s²).
   - The plan still only ever slows a push; it never pitches against the stick.
2. **`PushPlanKNeg` 1.4: the shed fit × 1.4 on the negative-AoA side.**
   - This sits just under the measured mean, because those measurements are averages over pushes the plan was holding back.
   - The sideslip allowance at the bottom is unchanged (8°). The plan just stops assuming the yaw sheds as slowly as it does from positive AoA.

**On "2 or 3 more AoS":** I left `PushoverSideslip` at 8.
- The plan wasn't held back by the allowance. It was held back by its slow catch-up. A larger allowance moved the bench replays by 0–0.1 s.
- With the faster catch-up, the positive pushes should arrive at the bottom with more yaw still on. Expect a couple of degrees more sideslip
  there, which is the trade you asked for.
- If you still want more after flying it, `6. Lateral governor / PushoverSideslip` takes effect at once (10–11 for +2–3°).

## Bench
**Replays (plan start → 10° AoA, and peak sideslip):**

| event | 0.12.0 | 0.13.0 |
|---|---|---|
| 09:16, 88.5 s (− → +) | 2.03 s, 2.1° | **1.65 s, 7.0°** |
| 00:59, 73.1 s (− → +) | 2.02 s, 2.4° | **1.83 s, 6.6°** |
| 01:53, 36.9 s (− → +) | 1.63 s, 3.6° | **1.50 s, 4.4°** |
| 00:23, 214.0 s (− → +) | 1.60 s, 2.0° | 1.55 s, 3.3° |
| 00:23, 232.2 s (− → +, held roll, 56 m/s) | 1.52 s, 3.0° | 1.48 s, 3.0° |
| 09:16, 104.2 s (+ → −) | 1.90 s, 9.8° | 1.82 s, 12.0° |
| 09:16, 82.3 / 94.0 s (+ → −) | 1.80 / 2.02 s | 1.80 / 2.00 s |
| 09:16, 98.7 / 109.3 s (− → +) | 1.77 / 1.73 s | 1.77 / 1.72 s |
| 00:23, 107.4 / 179.3 / 256.4 s; 01:53, 30.4 s (+ → −) | | same or 0.03 s faster, sideslip same or lower |

**Bench limits:**
- On the bench the positive-side yaw sheds slower than in the game (about 17 against 22–24 deg/s²). So the bench rarely reaches the point where
  the plan's catch-up binds, and the positive pushes barely move there.
- **The game's own numbers above are the case for the positive side:** the plan's need hit 60 mid-push in every positive push.
- At 109.3 s the bench's nose-up pitch response from negative AoA is weaker than the game's (it can't follow the faster plan). In the game the
  pitch rate tracked the plan there.

**Full suite (0.12.0 → 0.13.0):**
- Regression, stop/reversal sweep and corkscrew suites: **identical**.
- Damage suite: only the clean held pushes changed. At 560 / 600 km/h sideslip went 8.7 → 9.1° and 8.1 → 8.2°, and the AoA undershoot after the
  bottom went −40.5 → −35.5° and −46.7 → −41.8°. Every damaged-tail case is identical (the plan is off with the tails damaged).

## For the flight
- Corkscrew switches both ways, roll held and released: is − → + now as quick as + → −, and is + → − quicker?
- Sideslip at the bottom: the bench suggests about 4–7° on − → +; a little over 8° on + → −.

## Install
- `plugins\Ifrit_KR67.FCS\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` (md5 `c4904fa671b26f8c1bda65d6e2d2d955`) and the `meta.json` beside it.
- Release: `C:\Games\NO mods\Ifrit-FCS-0.13.0-release`. The source zip includes the bench.

## Flown (5 Nov 09:56 log; blend 0.65)
**Push-overs from 50–65° AoA:**

| Build | Time to 10° AoA (mean) | Mid-push rate dip | Pitch wobbles | Sideslip (mean) |
|---|---|---|---|---|
| 0.8.2 | 1.47 s | 2.8 deg/s | 1.1 per push | 6.7° |
| 0.12.0 | 1.62 s | 0.4 deg/s | 0 | 5.4° |
| **0.13.0** | **1.45 s** | 0.4 deg/s | 0 | **10.8°** |

**Sideslip:**
- With the roll released: 10.3–11.6° (0.8.x: 1.7–3.6°).
- With the roll held: 8.6–12.7° (0.8.2: 16.9–17.7°).

**Why it is higher:** the velocity roll is ramped down at the rate set at the push's start, |roll| ÷ the planned time T0. The faster plan now reaches the bottom before that ramp ends, with 9–25 deg/s of roll still on at zero AoA.

**Decision:** Tom (10:24): "nahh that sideslip is ok". Kept as is. The candidate fix, if wanted later: ramp the roll over the time actually left to 10° AoA, recomputed every tick.
