# F-22E FCS 2.26.0 — the pace from the first tick, the roll bounded at its release value, the rudders kept attached

Base: 2.25.0. **Compiled + bench-checked (full suite) + replayed on the bench from the logged states of the three 13:59 pushes and the
eight earlier ones, not flown.** No tuning values touched; no player or Dev keys. Game CSV 306 columns (as 2.25.0).

## What Tom flew (2.25.0, 13:59 log: three released pushes, 207–228 kt, 48–70 deg/s) — "still not fixed, why is it impossible to be smooth"
Every push had two stop-and-gos, both made by 2.25.0's own timing:
1. **At the top.** The stick went forward; the follow latch only engages once the AoA is falling (> 2 deg/s), so for 0.2 s the command ran
   −28 through the still-open kinematic cap and the nose reached −22 deg/s; then the AoA moved, the follow engaged and its pace (15 deg/s
   of AoA rate against +14 of flight-path rotation) took the command to −5: the nozzles swung −18 → +12, the nose was yanked back to −2,
   held 0.5 s, then went on at −38. 28.7 s: `qCmd` 7, −21, −28, −30, −24, −14, −7, −6, −5, −5, −6, −7, −8, −12, −17, −23, −32, −40.
2. **At the bottom.** The follow fades out 16 → 8° AoA and hands back to the kinematic sweep cap, which divides by the velocity roll the
   follow had built (60 deg/s of body roll at 8°: `capSwp` 32 → 27) — the command eased −33 → −15 at 8 → 2° and then went to −49. And the
   body roll had grown 41 → 62 deg/s between 20° and 8° (p = r cot α with the yaw shedding only 15–20 deg/s² below 30° AoA — see 4).
What 2.25.0 did do: sideslip at the bottom −8…−9° (−15…−21° on 2.24.0 at these speeds).

## Changes
1. **The follow and its pace engage on the push intent** — pitch stick toward zero AoA (`ReleaseFollowPushStick` 0.15), not on the AoA
   already falling — and **the pace is in force on any released stick at AoA with a yaw on** whether or not the latch has engaged
   (`wPace`), so the first tick's command is already the paced one. `ReleaseFollowPaceK` 1 → 1.5: at 1 the pace at the top sat under the
   flight-path rotation and the floor held the nose at zero body rate for 0.4–0.9 s; at 1.5 the nose starts at −5…−10 deg/s at once.
2. **The kinematic sweep cap is scaled out on the released stick at any AoA** (`ReleaseDropSweepRel`), not only while the follow is
   engaged: a stop in progress brakes the velocity roll, so the yaw the sweep must take out is less than pS cos α × AoA-rate and the cap
   over-constrained the bottom of every push. The shed cap (time to shed the yaw against time to zero AoA) stands.
3. **The follow's target is bounded at the body roll the jet had at the release** (`ReleaseFollowCapPS0`: p0 / cos α) — "the roll stops,
   a little slower is fine", never faster. The yaw the bounded roll cannot coordinate at the bottom is the rudders' and the 4° allowance's.
4. **`RudderWindowSweep` on.** The fins are canted, so at high body AoA each rudder sees a large local AoA from the cant alone. 13:59 log,
   29.9–30.1 s (25 → 18° AoA): the right rudder held at −30° with **29–34° of local AoA** (stalled, the peak is ~20°) while the left sat
   attached at −9°; the allocator's rudder commands flipped ±35 every 2–3 ticks on the plateau; the yaw shed 15–20 deg/s² against a
   capacity read of 50. During a nose-down sweep with a rotation over 12 deg/s, each rudder is kept on the attached side of its peak
   (`StallWindow`, as the stabs are in a rolling pull since 1.8.40 and in the capped sweep since 2.9.2). **The bench's rudders do not
   reproduce the game's plateau behaviour (replays ±0.1° with it on or off; in the suite reversals ±0.3°, one-fin pushes 1–3° better), so this one is flight-checked only** — the
   `Rudder_*_aoaNow` / `_cmd` / `_now` columns say what it did; if the sideslip or the yaw shed is worse with the rudders inside their
   windows, this is the one to take out.

## Bench
- Replays of the three 13:59 pushes, 2.25.0 → 2.26.0: body pitch rate one curve from the first tick (28.7 s: 16, 14, 11, 7, 2, −3, −8,
  −10, −11, −10, −9, −9, −11, −14, −18, −23, −26, −30 … −54; 2.25.0 on the same states: 12, 9, 5, −1, −6, −11, −15, −16, −15, −13, −10,
  −9, −8, −9, −11 …); command never positive; body roll bounded at the release value (peak 41/42/38 against 55/51/50 following the yaw
  unbounded); bottom sideslip 4 / 6 / 1° (the bench's yaw is not shed below 45° — its rudders — so this is the bounded roll's cost there).
- The eight earlier pushes, 2.25.0 → 2.26.0: start delay 0.87 / 0.17 s → 0.02 / 0.32 s, the rest unchanged; command reversals 1, 1, 0, 2,
  1, 2, 0, 0 → 0, 0, 0, 2, 1, 2, 0, 2; body roll at 15° AoA 53–67 → 27–53; bottom sideslip (bench) −10.8 → −7.8, ≤ top → −12.2, −8.6 →
  −4.3, ≤ top → −8.6 — the bounded roll pays where the bench's yaw is not shed.
- Full suite vs 2.25.0: see `suite_v226_vs_v225_*.diff` in the release folder (summary in the handoff §0).

## What to expect in flight
Released push with a rotation on: the nose starts down at −5…−10 deg/s the instant the stick moves and goes on down one curve as the yaw
is shed; the body roll holds about its value at the release and comes to rest with the yaw; no nozzle reversal. If the yaw sheds slower
than the capacity read says, the shed cap eases the nose (a slowdown, not a stop) and the bounded roll lets a few degrees of sideslip
accrue at the bottom. Held pushes as 2.25.0 (one curve, a possible ~0.2 s ease to zero body rate at 40–30°).
`LogOutput.log` must say 2.26.0; 306 columns; `relFollow`, `capDn`, `pSTarget`, `Rudder_L/R_aoaNow`, `Rudder_L/R_cmd`.
