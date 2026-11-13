# F-22E FCS 2.29.0 — the followed roll is the coordinated roll, unbounded; the pace during the release dwell

Base: 2.28.0. **Compiled + bench-checked (rev / reg / dmg blocks) + replayed from the 15:13 pushes, not flown.** CSV 306 columns.

## What Tom flew (2.28.0, 15:13 log) — "both held and released push-over are rougher again; no-fins at high AoA and push-over better"
- Released 32.5 s (215 kt, 73 deg/s, roll stick coming off with the push): command 6, −16, **−47, −49, −30**, −25, −26, −31, −32, −28, −24,
  −16, −10, −4, **−1**, −3, −9, −17, −23 … Two things. (1) The first 0.2 s ran on the held pace while the stick was inside the release
  band but not yet counted released (the 0.12 s dwell), then the released pace took it back: the spike. (2) At 33–25° the follow target
  was bounded at the body roll the jet had when the stick came off (28) while the airframe rolled itself to 57 toward coordination
  (63): the roll loop fought it at −200 deg/s² of unmet, the capacity read fell 55 → 20, the released pace 60 → 29, the nose stopped at
  30°. 2.24.0's capacity collapse again, this time made by the 2.28.0 bound.
- Held 78.5 s (198 kt, 64 deg/s): −26 → −41 (0.5 s), −35, −25, −17, −10, −6, −3, −1, **0, 0** (0.3 s), −2, −4, −7, −12, −19, −24. The
  trade cut the roll target 75 → 33; the airframe rolled to 74 anyway (unmetX −256) and the rotation went 68 → 83 deg/s; the pitch is
  bounded by the rotation the jet has (`TradeCapShare` 0.7 × capacity 35 / (83 cos α)) = 19–21 deg/s of AoA rate against +17…+22 of
  flight-path rotation → zero body rate. Sideslip −3° of the 4° margin. That is the law doing what it says (both axes the bottleneck
  → the pitch gives); the ease is set by the 0.7 share of a 35 deg/s² read — see "what is his" below.

## Changes
1. **The followed roll is the coordinated roll, r / sin α, unbounded.** The 2.28.0 bound at the release body roll was "the roll never
   accelerates in a stop" — but the airframe accelerates it toward coordination by itself through 50–35°, and a target under that is a
   fight that collapses the capacity read and stops the nose. The pace keeps the roll from *needing* to grow (`ReleaseFollowPaceK` 1.5);
   what the airframe adds on its own the loop no longer fights. Consequence, stated plainly: at the bottom of a fast push, with yaw
   still on at 15–10° AoA, the body roll will come up to what coordination needs (the 15:13 push had 19 deg/s of yaw at 11° → ~90
   deg/s of coordinated roll) and stop with the yaw. The bench shows exactly that (body roll 72–99 at 10°). If the roll at the bottom
   is unwanted, the honest lever is `ReleaseFollowPaceK` toward 1 (the nose then paces so the roll needn't grow at all — slower
   nose), not a bound on the roll.
2. **During the release dwell the pace is the tighter of held and released** — a roll stick coming off together with the push no longer
   gets 0.2 s of the open held pace first.

## Bench (vs 2.28.0)
Reversals identical. Regression: 65° releases 2.8/2.9 → 2.4/2.5, pullthrough-released 350 6.2 → 8.2, rest unchanged. Damage: clean
released pushes 10.2/10.5 → 9.7/9.6 (2× bench, half-yaw leaf), held clean unchanged, one-fin released 560 7.1 → 13.0 (the unbounded
follow with one fin; 600 9.3 → 10.0), one-fin held 8.9 → 8.5, one-fin leaf stops 1.7/1.9 → 1.6/1.1, no-fin cases unchanged.
Replays of the 15:13 pushes: 32.5 s command −20, −16, −17, −19, −21, −21, −22, −24, −23, −23, −25, −27, −29, −30, −28, −25, −22, −19,
−18, −21, −25 … (one curve, sideslip ≤ 4); 133.4 s −23, −18, −15, −16, −17, −18, −17, −17, −17, −19, −22, −25, −29, −32, −34, −34, −34,
−32, −29, −25 …; held 78.5 s −36, −37, −38, −38, −38, −36, −25, −18, −12, −8, −5, −3, −2, −4, −6, −8, −13, −20, −25, −27 (the ease
stays: it is the rotation bound).

## What is his, not the law's
The held push's ease is `TradeCapShare` 0.7 × the shed-side capacity read (35 deg/s² there, with the yaw actually delivering 27–30
and the airframe adding rotation) against +20 of flight-path rotation. Raising the share lets the held push plan on more of the
capacity and use more of the 4° margin (sideslip was −3). It is an internal static today; say the word and it becomes a key, or a
value. `PullRollTrade` 0.7 is untouched.
