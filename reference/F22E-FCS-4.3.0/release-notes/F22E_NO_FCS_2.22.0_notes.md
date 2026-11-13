# F-22E FCS 2.22.0 — the push-over paced to the shed it will keep, not the shed it has

Base: 2.21.0. **Compiled + bench-checked, not flown.** Law change in the shed cap (the released-roll push-over's pace),
hence the minor bump. No tuning values touched; no player or Dev keys. Game CSV: 297 → **302 columns** (`ceCapNsP`,
`ceCapNsN`, `ceWdNs`, `sweepNsW`, `aYawShed` appended after `ceCapTrN`).

## The 10:40 flight (2.21.0) — "the TVC wanted full nose-down at first, then the cap caught up and it immediately slowed down"
Every push-over from 65° in the log does it; 43.6–44.5 s (250 → 180 kt, r = −37 deg/s, roll released before the push):

| | 65° → 57° (43.6–44.0 s) | 57° → 45° (44.0–44.3 s) |
|---|---|---|
| pitch on | nozzles (`stabPitchFrac` 0) | stabs coming back (0 → 1 at 44.1–44.2) |
| measured yaw shed `ceWdZ` | 7 → 24 deg/s² | 24 → 11 |
| cap's allowed AoA rate | ~30 deg/s | ~18 |
| `qCmd` | −24…−32 | −23 → −14 → −9 → −3 |
| AoA rate actually | −12 → −39 | **−43…−49** (trajectory still at −31) |
| TVC | −5 → −16 | **+1 → +19** (full nose-up) |
| body pitch rate | +7 → −29 | −32 → −24 |

The cap paces to the yaw the airframe sheds **now**. At 65° the pitch is on the nozzles and the stabs' differential is most
of the yaw axis, so the shed reads 22–33 and the cap allows ~30 deg/s of AoA rate. Every push from there passes the AoA
where the nozzles stop carrying the pitch (~55°, 0.3 s in); the stabs go back to it, the yaw loses them, the shed halves,
and the same arithmetic says ~18. By then the sweep is running at 43–49 (the trajectory's 0.3 s lag on a cap that just
halved), so the cap commands a *positive* body pitch rate to brake it — the nozzles reverse to +19 and the nose stops
while the AoA goes on falling at 40 deg/s. The 09:53 log (2.19.0, 36.0–37.5 s) has the same signature; it is not new to
2.21.0 (the 2.21.0 changes are nose-up only; `tradeAlpha` is 0 throughout these).

## Change
**The shed cap reads the yaw without the stabilators while they are off the pitch.** `CapacityEstimator` adds the
no-stab composites `capZeNsP/N` (measured yaw less the stabs' modelled yaw at `now`, committed and probe less the stab
group, same unmet, same rudW blend) and `wdZNs`. In `LateralLaw` the shed cap's `capShedSide` and `shedMeas` are blended
to those with weight `(1 − stabPitchFrac) × finKc`: all the way at 65°, nothing once the stabs are back on the pitch
(where the allocator has already taken their yaw away). Clean jet only — without the fins the stabs *are* the yaw axis
and the 2.19.0 nose-hold owns that case (`finKc` 0 → weight 0). Static `SweepShedNoStab` (on) is the bench A/B.
The push then starts at the rate the rest of the sweep can keep, and there is nothing for the cap to catch up to at 55°.

## What "smoothly slowing down" will look like, and what it cannot
The cap's output is an AoA rate; the body pitch rate the pilot feels is that plus the flight-path rotation, and the flight
path turns faster as the AoA falls through the lift peak (this flight: `wVe` +9 at 65° → +15…+19 at 40–45° at 2–3 g).
Holding ~18 deg/s of AoA sweep from 45° down therefore means a body pitch rate near zero and the nozzles near neutral —
the nose stops turning while the AoA keeps falling at the paced rate. That part is physics and stays; what goes is the
yank — the trajectory running 25 deg/s past a pace that just halved, and the +19° nozzle swing that brakes it. If a
faster push from 45° down is wanted, `PushoverSideslip` (4°) is the knob, at the sideslip it costs.

## Bench
The bench cannot reproduce the game's push-over from the leaf: its yaw capacity in the leaf is about half the game's
(replay of 43.6 s: its cap sits at the 6 deg/s floor from the first tick on either build; the game ran −30), and in the
replay of 52.1 s the stabs' share of its capacity at the top is small (`aYawShed` 19–26 with the no-stab read against
14–30 without), so the change shows only as ±8 deg/s of command in the first 0.3 s and the same hold at 35–45°. The
handoff has said since 2.8.1 that the leaf's yaw at the push is a flight question; it is. The clean-jet suite vs 2.21.0
is in the release folder's `suite_v222` diff (push-over cases are where it may move; everything nose-up is untouched by
construction).

## What to fly
Released-roll push-overs from 65° at 180–250 kt. The pass: the nozzles do not reverse at ~55°; the body pitch rate
eases from its first value to about zero without a positive excursion; `qCmd` is one monotonic curve from the push to
~40°. The sweep from 65° to 45° will take longer than it did. Held-roll push-overs and the leaf itself should not move.
