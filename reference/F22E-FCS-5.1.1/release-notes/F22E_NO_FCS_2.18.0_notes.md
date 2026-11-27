# F-22E FCS 2.18.0 — the window never backs the target up; without fins the nose waits above the fade and the roll follows the yaw down

Base: 2.17.0. **Compiled + bench-checked, not flown.** 2.17.0 kept as `.dll.2.17.0.bak`. Three law changes, all inside existing
devices; two of them scaled by fin health (inert with the fins on). No tuning values touched. Statics for the A/B:
`PaceNoRetreat`, `SweepHoldFade` (+ `SweepHoldFadeMargin` 0, `SweepHoldBlend` 5), `ReleaseCoordinate`, all on.

## The 23:49 flight (2.17.0, both fins off at 39 s)
- The 2.17.0 lead change removed the freeze: the 52.5 s reversal's target moved the moment the stick did (zero crossing 2.5 s, no
  backward run). The 59.8 s reversal still ran the old way for 1.2 s — surface by surface: 0.3 s of correct action took the body
  roll 6 → 1 deg/s, the yaw (30 deg/s) still implied a 33 deg/s rotation, the band set the target at rotation − lead = 25 deg/s,
  25 deg/s *coordinated* means p = 10.6, so the roll loop drove +X (tgtX +47…+58 against a −30 demand) and the allocator gave it
  the stabs and flaperons (roll weight 1.5 vs yaw 8). Once the target came down the yaw braked at 24 deg/s². Tom rejected the
  same behaviour in 2.7.0 ("keeps rolling the old way for a moment then reverses"); the 2.7.2 latch brought it back where the
  yaw is weak.
- Push-overs: held from 61° with 40 deg/s → 20.6°; held from 60° with 46 → 41°; released from 64° with 30 → **53°** (roll
  target 9 → 0 in 1.5 s while r stayed 29 → 22; the yaw shed 2.3 deg/s²). The pitch was already paced to 5–9 deg/s of AoA rate.
  Without fins the airframe is directionally unstable below ~35–40° and the yaw left there is 2–8 deg/s²; the only place the
  rotation can be shed is above ~45° (20–28 deg/s²). Tom: "with the fins, when gravity pulls the jet sideways it is stable and
  easily recoverable; without fins the jet falls like a brick."
- The leaf at 65° without fins: |β| ≤ 6° in every stop and reversal. The limit that matters is not an AoA limit but the
  rotation on at the push: 28 deg/s → 18°, 30 → 20°, 46 → 41°, 48 → 81° (two flights). ≈ 0.7° per deg/s.

## Changes
1. **`PaceNoRetreat`** (window): the latched band may hold the target where it is; it may never move it back toward the
   rotation, away from the stick's demand. The target only ever moves toward the demand.
2. **`SweepHoldFade`** (shed cap): with the fins gone (`yawHealth` < 0.5, scaled), (a) the coordination credit of the held
   rotation fades out — a rotation cannot be sustained below the fade without fins, so all of it is excess for the sweep —
   and (b) while excess yaw remains the AoA-rate cap goes to zero over 5° above `RudderFadeStartAoA` (50°): the nose waits at
   the fade until the yaw is under `SweepShedDead` (5 deg/s). Roll released: 1.5–4 s at the flight's 11–24 deg/s² of shed.
   **Roll held: the nose stays at ~50° until the roll is released** — a held corkscrew cannot be pushed through the fade
   without fins, by the airframe. The credit now reads the stick's own target (`pSStickRaw`, before the 2.16.0 floor), as 2.10.0
   intended.
3. **`ReleaseCoordinate`** (lateral limit): stick released under a capped nose-down sweep with the fins gone, the velocity-roll
   target is the roll the yaw implies (r / sin α, decaying as the yaw sheds) instead of zero — the roll follows the yaw down.
   Scaled by fin loss; with the fins the rudders shed the yaw in tenths and the stop stands.

## Bench (sideslip ~2× high), 2.17.0 → 2.18.0
Clean jet (only no-retreat acts): three rolling pulls +1–2° (pull-then-roll 320 5.7 → 7.7, 400 5.1 → 6.9, fullaft+roll 250 4.4
→ 5.4), corkscrew entries +0.5–2°, reversal times unchanged, the hold at a 65° release gone (`pace` 6.9 → 0.9); the rest ±0.5.
Utilisation invariant 4 FAIL / 9 as before. Damage suite: no-fin held push 560 km/h entry 47 → **12.8°**, released 66 → 30°
(the nose waited, aMin 21–29° instead of 2°); 600 km/h entry 16 / 20 → 49 / 61° — the nose held at 44–47° with 46 deg/s on
and the bench's 3 deg/s² of shed never cleared it: **the bench airframe sheds a third of the real yaw, so this case is the
flight's to judge**; no-fin leaf 5–7 → 9–11° (no-retreat; one stop now completes in 3.3 s, the others never, same plant
reason); one fin ±0.5; fins + flaperon at 480 kt 25 → 38° (departed in flight too, 18:25 log).

## What to expect without fins
Reversals: the surfaces no longer come back the old way, ~1.3 s to cross, ~5° more leaf sideslip than 2.17.0's 4.7°. Push-overs:
released — nose waits at ~50° until the rotation is gone, then comes down with the roll following; held — nose waits at ~50°
until the roll is released. If the leaf sideslip is too much for the crispness, `PaceNoRetreat` is the one to take back out.

## For the next log
Both fins off: reversals and stops at 65°, a released push from 65° with 30–45 deg/s on, a held push (expect the nose to stop at
~50°), then release the roll and watch it come down. Clean: one corkscrew entry at 300–350 kt. `LogOutput.log` must say 2.18.0.
