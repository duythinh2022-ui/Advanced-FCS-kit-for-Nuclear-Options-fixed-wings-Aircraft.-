# F-22E FCS 2.25.0 — one smooth pitch pace, and the released roll follows the yaw down

Base: 2.24.0. **Compiled + bench-checked (full suite, bisected one change at a time) + replayed on the bench from the logged
states of eight pushes of the 13:06 / 13:12 / 12:16 / 11:19 flights, not flown.** No tuning values touched; no player or Dev keys.
Game CSV: 304 → **306 columns** (`relFollow`, `capDn` after `aYawShed`).

## What Tom flew (2.24.0, 13:06 and 13:12 logs) and asked
"Not better or worse, just different: sometimes it steps, sometimes it goes all the way to the hard stop then goes again, for both
the released push-over and the held push-over. Why does it seem impossible to just make the pitch rate slower during the push-over,
and the roll stops during the pitch-down?" Then: "it's OK if the roll stops a little slower during the slower pitch-down, but it
needs to be smooth and the sideslip needs to be in check."

Two things in the logs, both his:

1. **The cap chattered.** Both nose-down AoA-rate caps were recomputed every tick straight from the capacity estimator's read
   (probe − allocator unmet; the no-stab blend switching as the stabs return to pitch; 2.24.0's delivered term rising and falling)
   and the lower one clamped the pitch command tick by tick. 38.7 s push (210 kt, 64 deg/s released): `capSwp` 23, 17, 20, 24, 28,
   30, 30, 22, 20, 22, 21, 13, 13, 15 against +20 deg/s of flight-path rotation → `qCmd` −11, +2, 0, −3, −8, −10, −2, then **0 for
   0.4 s** (the hard stop), then −3 → −60. 164.6 s (227 kt, 73 deg/s): `qCmd` −34, −26, −11, −37, −43, −29, −20, −36, −20, −6, −6,
   −10, −19, −30, −43 with the nozzles −20 → −6 → −20 → −2 → −20. Every build since 2.21.0 changed *which* jumpy number fed the
   cap; none made the cap itself continuous.
2. **The released stop braked the roll away from the yaw.** With the stick released the velocity-roll target went to 0 in 0.15 s
   (`pSTarget` 25 → 0) and the roll loop fought at −250 deg/s² of unmet for a full second against a body roll that is only the
   rotation re-expressing itself as the AoA falls (p = pS cos α rising by itself); at ~40° the flaperons braked p 62 → 14 at
   −100 deg/s² while the rudders, at their 35° stop, braked the yaw 50 → 26 at −48 — sideslip −2° until the roll stop, −19° by
   15° AoA (63.1 s push, 218 kt). Stopping the *velocity* roll means braking roll and yaw together, at the yaw's pace.

## Changes
1. **`ReleaseFollowYaw`** — with the lateral stick released (for ≥ `ReleaseFollowDwell` 0.15 s), |AoA| ≥ 16°, the yaw ≥ 5 deg/s
   and the AoA falling, the velocity-roll target is the coordinated roll for the yaw the jet still has, r / sin α, inside the lateral
   limit raised to it (2.16.0's rule: a limit bounds what the stick may build, not a rotation the jet has); it fades out 16 → 8° AoA
   (the rudders own the yaw there) and drops when the stick comes back or the yaw is gone. The pace window and the steady-rate cap may
   not pull the target below it. The yaw loop is asked for its brake capacity directly (rCmd = r − capacity / KYaw, the 2.20.0 no-fin
   stop's rule) because the coordinated target alone asks it for nothing. Weighted by the fin health (`ReleaseFollowFinPow` 1: one
   fin = half, no fins = off — the no-fin push keeps its 2.19.0 hold and 2.18.0 release coordination). While it is engaged the
   kinematic sweep cap (`capSweep`) is scaled out — with the roll following, the sweep's coordination is the roll axis's — and a
   **follow pace** joins the shed cap: p = r cot α grows as the AoA falls unless the yaw is shed as fast, so the AoA rate is held to
   a · sin α cos α / r (`ReleaseFollowPaceK` 1 = the body roll never rises above its value at the release; the physics of "the roll
   stops during the pitch-down"). At the top (65–55°) that pace is under the flight-path rotation, so the no-reverse floor holds the
   body pitch rate at zero for 0.4–0.9 s while the AoA bleeds at 8–20 deg/s; the nose then follows one curve down.
2. **One continuous cap.** The capacity reads the two caps divide by are low-passed over `SweepCapTau` 0.2 s (a capacity cannot
   physically change faster than the surfaces move) and the resulting AoA-rate cap may tighten no faster than `SweepCapSlewDn` 150
   and loosen no faster than `SweepCapSlewUp` 100 deg/s per second. Logged as `capDn`.
3. **`SweepNoReverseHeld`** — the 2.24.0 zero floor on the held push too (13:12 log, 122.0 s: `qCmd` −25 → 0 → **+7** → −36 with the
   stick full forward; body rate +4). Bench: no change on any held case.

Kept in source, **off**: `RudderWindowSweep` — the fins are canted, so at 65° body AoA each sees ~29° of local AoA from the cant
alone, and in every push the allocator's rudder commands flipped sign tick to tick on the stall plateau (13:12 log 165.0–165.4 s:
`Rudder_R_cmd` −14, +24, +35, −35, +14, −35, −35, +35 …; the surfaces wandering at 0–20°; 13:06 log 63.8 s: the left rudder sent to
−35 at 57° of local AoA). Keeping each rudder on the attached side of its peak during the sweep (as the stabs are since 1.8.40/2.9.2)
is the obvious candidate for the yaw brake the push depends on, but the bench's rudders do not reproduce the game's behaviour there
(replays ±0.1° either way), so it cannot be checked here — flight only, one change at a time.

## Bench (bisected: full 2.25.0, without the follow, without the held floor, without the smoothing; final suite vs 2.24.0)
- Reversals, corkscrew entries, utilisation (4 FAIL / 9): **identical**. Regression set within ±0.5° except push-then-pullthrough
  450 6.3 → 7.5 (the smoothing's lag at the crossing) and pullthrough-released-at-zero 350 6.4 → 5.7.
- Damage suite: **clean released push-overs 560 km/h 10.4 → 7.5°, 600 15.1 → 8.5°** (now the top-of-leaf value; after the push 5.4 /
  4.2); held clean unchanged (7.5 / 8.5); no-fin pushes 20.2 → 18.0, 13.9 → 12.8 (the smoothing); one-fin released 2.2 → 5.6, 6.7 →
  7.6 (the follow at half weight; at full weight 10.0 / 12.6 — the fin scaling is what made that acceptable), one-fin held 4.8 → 5.7;
  leaf stops/reversals ±0.5; fins + R flaperon reversal 39.2 → 36.9; LERX-off hold 5.2 → 3.1.
- First cut's dwell-less latch caught a reversal's stick crossing zero (0.5-pull roll reversal 400: t90 1.27 → 1.67 s, push-then-
  pullthrough 450 6.3 → 7.5 from the follow) — the 0.15 s dwell removed it. Smoothing variants: no loosen slew ≈ same; τ 0.1 ≈ same;
  filter without slews worse (pullthrough released 8.4).
- Replays of eight pushes from their logged states (bench: sideslip ~2×, leaf yaw ~½, **and its rudders sit at ±8° through 45–15°
  AoA where the game's go to the stop — the bench does not shed the yaw in the bottom half of a push, the game did at 40–50 deg/s²**).
  Command reversals > 4 deg/s between consecutive extrema during the sweep, 2.24.0 → 2.25.0: 8 → 1, 6 → 1, 3 → 0, 6 → 2 (held), 7 → 1,
  7 → 2 (held), 3 → 0, 11 → 0. Most positive command: 0.0 / −0.3 / +0.1 / **+3.0** / −1.4 / **+5.9** / −15.6 / 0.0 → 0.0 / 0.0 / 0.0 /
  −10.8 / −2.7 / 0.0 / −1.0 / 0.0. Bottom sideslip (bench): −11.6 → ≤ top, −21.9 → −10.8, −20.6 → ≤ top, +9.2 → −8.6, −14.4 → ≤ top;
  the 12:16 push 5.3 → 6.5. Time 65 → 15° within ±0.1 s of 2.24.0 except 2625 1.28 → 1.53 s. **Body roll at 15° AoA 50–67 deg/s on the
  bench** (its yaw not shed, so the roll goes on following it) against 0–7 on 2.24.0 — in the game, with the yaw shed as the 2.24.0
  flights did from 40° down, the follow target falls to zero with it; the flight decides.

## What to expect in flight, plainly
- Released push from 65° with a rotation on: the nozzles move at once but the nose holds about zero body pitch rate for 0.4–0.9 s
  (the AoA is already falling at the flight-path rate), then one smooth nose-down to the bottom; **the jet keeps rolling at roughly
  the release's body roll rate through the push and stops when the yaw is gone**, sideslip staying small instead of the −15…−20° at
  the bottom. If the yaw sheds slower than the 2.24.0 flights showed, the roll goes on longer (not faster), and the pace holds the nose.
- Held push: one curve; at 40–30° the nose may ease to zero body rate for ~0.2 s with the AoA still falling (the kinematic cap at
  half the capacity, 2.23.0's bisect) and go on — nothing turns the nose back.
- Columns: `relFollow` (±1 while the follow is engaged, × its fade), `capDn` (the cap the command actually obeyed), `pSTarget` (the
  coordinated target), `capSwp` / `capShd` as before. `LogOutput.log` must say 2.25.0; 306 columns.
