# F-22E FCS 2.24.0 — the released push paced to the coordination the jet is measurably keeping, and a cap that never turns the nose back

Base: 2.23.0. **Compiled + bench-checked + checked on the game's own logged states of all 18 push-overs of the three 3 Nov
flights, not flown.** Two changes to the nose-down sweep cap, both for the released-stick push only; the held-roll push keeps
2.10.0's accepted pace unchanged. No tuning values touched; no player or Dev keys. Game CSV: 303 → **304 columns**
(`sweepDel` between `capShd` and `aYawShed`).

## The 12:16 flight (2.23.0) — "it just gets worse after every round"
Correct, and the cause is now read from the two cap columns 2.23.0 added. The 26.25 s push (194 kt, 57 deg/s of yaw,
velocity roll 63, roll released):

| | 26.3–26.8 s | 26.85–27.2 s | 27.2–27.6 s |
|---|---|---|---|
| AoA | 65 → 56 | 56 → 31 | 31 → 21.5, **then stops at 22° for 0.4 s** |
| `qCmd` | −14 → −43 | −43 → −27 → −10 → **+9 → +15** | +15 → 0 → −44 |
| nozzles | −8 → −17 | −20 → **+20** (held 0.5 s) | +20 → −20 |
| body pitch rate | +14 → −26 | −32 → −49 → −33 | −28 → **+11** → −3 |
| sideslip | 3.9 → 1.0 | **1.0 → 0.3** | 0.3 → 1.2 |
| `capShd` | 60 (open) | 60 | 60 |
| `capSwp` = 0.5 × `ceCapZeN` / (pS cos α) | 36 → 50 | 52 → 43 → 34 → 22 → 14 → **8** | 8 → 16 → 46 |
| `ceCapZeN` | 51–56 | 56 → 38 → 30 → 20 → **12** | 12 → 28 → 40 |
| `unmetX` / `unmetZ` | −50 / −30 | **−250** / −60 | −190 → −30 |

The sideslip was 1° throughout; nothing lateral was in trouble. The kinematic cap stopped the nose because the capacity it
divides by fell from 56 to 12 while the rudders sat at 5–14° of 35°: the roll loop, target 0 with the stick released, was
trying at full authority to kill a body roll that is only the rotation re-expressed as the AoA falls (p = pS cos α: 14 → 44
while pS stayed 37–51) — it never braked it, p *rose* at +70 deg/s² because the airframe rolls itself into coordination —
and its fight is what the allocator reports as unmet, which the estimator subtracts from the probe. The faster start
2.23.0 added (−65 deg/s of AoA rate instead of −42) made the collapse deeper and the brake harder. The predicted capacity
was blind to the coordination the airframe was delivering by itself: measured wind-yaw deceleration ṙ cos α − ṗ sin α =
58 deg/s² against a need of pS × AoA-rate = 52; the estimator said 12.

## Changes (both released-stick only: weight `stickRelW` = 1 − |lateral stick| / `SweepNoReverseStick` 0.2)
1. **`SweepDelivered`:** the kinematic sweep cap's capacity is max(`SweepYawK` × the estimator's prediction, the wind-yaw
   deceleration the jet is measurably delivering toward the shed side), the measured term from the estimator's filtered
   body accelerations (`wdZ`, `wdX`), low-passed over `SweepDelTau` 0.1 s, logged as `sweepDel`. While the rotation stays
   coordinated the cap cannot tighten below the rate the airframe is demonstrably keeping up with; it tightens only on a
   measured deficit. (The derivation: d(rS)/dt = (ṙ cos α − ṗ sin α) − pS × AoA-rate; the drift is −pS × AoA-rate, the
   delivered counter is sign(pS × AoA-rate) × (ṙ cos α − ṗ sin α).) `SweepYawK` stays 0.5 on the predicted part (the 2.23.0
   bisect: 1 doubled the bench's push sideslip by running the pitch demand past the plant).
2. **`SweepNoReverse`:** a cap on the sweep toward zero AoA may hold the nose, never turn it back — the pitch-rate command
   it leaves is bounded at zero (the AoA still falls at the flight-path rate). Where the deficit is real (11:19 log, 223–244 kt
   with 70–80 deg/s on, sideslip 5–6° and climbing) the honest cap of 14–19 deg/s of AoA rate sat under +20…+24 of
   flight-path rotation = a nose-UP command of +5…+18 with the stick full forward. Not under the 2.19.0 no-fin nose-hold
   (holding the AoA on a turning flight path IS a positive body rate; with the floor there the no-fin held pushes departed
   on the bench) and not with the roll held (the nose waiting under a held roll is 2.10.0's accepted behaviour; the floor
   there took the bench's clean held 560 case 7.5 → 12.6).

Tried and removed in this round: a one-sided slew on the cap's tightening (250 deg/s per s, to take out one-tick dips of
the raw capacity read) — one-fin held push 560 4.8 → 11.3 on the bench, the no-fin leaf cases moved; at 600 neutral but
worth little. The dips remain as 1–3-tick steps of 8–15 deg/s in the command on the bench replays (the capacity read jumps
~20 as the rudders come back at ~40°); they are not reversals.

## Checked on the game's own states (what the last two builds did not have)
The caps reconstructed tick by tick from the logged columns of every released push-over of the 10:40, 11:19 and 12:16
flights (18 pushes, 155–250 kt, 36–80 deg/s), stick more than half forward, AoA 12–62°: the most positive pitch command the
2.24.0 law gives on those states vs what the flown build commanded —
12:16: +15 → −1, +11 → 0, +3 → −6, +3 → −2; 11:19: +12 → 0, +19 → 0, +7 → +2 (pre-push nose-up momentum, stick not yet
full), +8 → 0, +4 → −5, −18 → −16; 10:40: +10 → +1 (same), +9 → −5, +15 → 0, +9 → 0, +14 → 0, −0 → −12, −0 → −7, +3 → −2.
On the four 12:16 pushes the measured term keeps the cap open (−45 where the flown build gave +15); on the 70–80 deg/s pushes
at 220–245 kt the cap still comes to the floor — body pitch rate held at zero, not reversed — because the deficit there is
real (sideslip 5–6° climbing).

## Bench
- Suite vs 2.23.0 (`bench.sh diff`): reversals, corkscrew entries, utilisation **identical**; regression set identical but
  `pullthrough, roll released at zero 350` 6.4 → 6.6; damage suite identical on every no-fin, one-fin and held case
  (the released-stick scoping is what made them identical — before it the floor broke the no-fin hold and the measured term
  opened the held pushes) **except the clean released push-overs: 560 km/h 7.5 → 10.4°, 600 9.2 → 15.1°** (the 2× bench).
  That is the measured term's cost and it is the same mechanism as every earlier bisect: the mid-sweep hold at 43–27°
  (the hitch) was absorbing the bench's roll-stop sideslip at the bottom. Tom's rule puts the hitch first; the stop is the
  lateral law's and is next.
- Replays of ten pushes from the three flights from their logged states (bench: sideslip ~2×, leaf yaw ~½): the command
  never exceeds 0 (on the four 12:16 pushes it never reaches 0: maxima −4.8…−12.6); the AoA sweep never stalls (slowest
  23–56 deg/s between 45 and 20°, against 2 deg/s at 22° in the game today); no nozzle reversal.

## What is left, and whose it is
- At 220–245 kt with 70–80 deg/s on, the push starts slowly (command −5…−10 for ~0.5 s while the measured term builds from
  zero) and the nose comes to zero body rate once mid-sweep when the deficit is real — slower, never reversed. The yaw
  cannot take that rotation out faster without sideslip; `PushoverSideslip` does not reach the kinematic cap.
- The bottom sideslip of those pushes (10–14° in the 11:19 flight) is the released roll stop at 40–30° — roll braked at 3×
  the yaw's rate — the lateral law's (§4 (a)/(b) of the handoff), and the bench says it grows when the nose no longer pauses.

## What to fly
Released-roll push-overs from 65°: 150–200 kt with 35–60 deg/s on (the 12:16 cases — the push should go straight through,
no stop, no nozzle reversal, `sweepDel` in the CSV at 20–60 while `capSwp` stays above 40), and 220–250 kt with 70–80 on
(expect a slower start and one smooth easing to zero body rate mid-sweep, never nose-up; `capSwp` tells which cap bound).
`LogOutput.log` must say 2.24.0; the CSV has 304 columns.
