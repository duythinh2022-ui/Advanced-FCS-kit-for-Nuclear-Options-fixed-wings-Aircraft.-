# F-22E FCS 2.23.0 — the push paced to the capacity from the first tick (the TVC no longer waits)

Base: 2.22.0. **Compiled + bench-checked, not flown.** One law value changed on the nose-down sweep (`SweepShedMeasFloor`
0.4 → 1), hence the minor bump; two telemetry columns added. No tuning values touched; no player or Dev keys. Game CSV: 302 →
**303 columns** (`shedSlipLeft` of the never-flown 2.22.0 layout does not exist; `capSwp`, `capShd` sit between `ceWdNs` and
`aYawShed`). The source tree's bench scripts are path-independent from this version (`bench.sh`, `bench/PORTABLE.md`) — the
same tree is the portable bench put on Tom's PC.

## The 11:19 flight (2.22.0) — "was the TVC waiting for the roll to stop so it could pitch down?"
Six released-roll push-overs from 65°, 186–244 kt, 35–80 deg/s of body yaw. The 47.07 s one (223 kt, r 74, velocity roll
87 deg/s, roll stick released 0.1 s before the push), reconstructed tick by tick from the logged columns (the reconstruction of
both caps matches `qCmd` within a few deg/s):

| | 47.10–47.35 s | 47.35–47.5 | 47.5–48.1 | 48.1–48.6 | 48.6–49.2 |
|---|---|---|---|---|---|
| AoA | 66 → 63 | 63 → 58 | 58 → 39 | 39 → 31 | 31 → 0 |
| `qCmd` | **0.2 / 0.1 / −0.5 / −1 / −3** | −9 → −22 | −22 → −8 → **+5…+12** | −1 → −25 | −40 → −60 |
| body pitch rate | +9 → +2 | −3 → −10 | −18 → −23 → −11 → **+13** (48.2 s, 38°) | +13 → −8 | −12 → −57 |
| AoA rate | −4 → −20 | −24 → −34 | −37 → **−42** → −16 | −13 → −7 → −34 | −38 → −56 |
| velocity roll pS | 87 → 82 | 80 → 77 | **76 → 79 → 75** (never braked) | 71 → 18 (−115 deg/s², rudders back) | 13 → 3 |
| binding cap | shed cap (measured shed 0 → 23, floor 0.4 × 19) | shed cap 29–36 | **`capSweep`** (0.5 × 57.3 × `ceCapZeN` / (pS cos α)) 15–21 | shed cap, opening | — |
| sideslip | 0.6 → 3 | 3 → 4 | 4.3 → 4.8 | 4.5 → −2.8 | −3 → **−11.3** |

1. **Yes, the TVC waited — on its own measurement, not on the roll.** The shed cap paces the AoA rate to the yaw deceleration
   it measures. At the push nothing was decelerating (`ceWdZ` +30 → 0: the yaw was still being built by the roll input
   released 0.1 s earlier), so the cap sat on its floor, 0.4 of the no-stab capacity (19) = 7–8 deg/s² → 7–9 deg/s of AoA
   rate, and the +11 deg/s of flight-path rotation ate that: a pitch command of 0 for 0.25 s with the stick at −0.6…−0.9.
   The "shed" that then let it open is the sweep's own consequence: a 77 deg/s velocity roll re-expresses into body roll as
   the AoA falls (r = pS sin α within 2 deg/s the whole way, p 46 → 57) — pS itself never slowed before 42° (`unmetX` −400,
   `unmetZ` −160: the lateral axes have nothing at 65–45°). So the cap bootstraps from zero and the trajectory overshoots it.
2. **The mid-sweep brake was the other cap.** From 47.5 to 48.1 s `capSweep` — the kinematic demand, a coordinated rotation
   pS needs pS cos α of body yaw per unit of AoA, at `SweepYawK` 0.5 of the capacity — read 15–21 deg/s while the shed cap
   read 18–58. The capacity dips 44 → 26 as the stabs go back to pitch, and cos α grows as the AoA falls with pS still at 76;
   15–21 of AoA rate less 20–23 of flight-path rotation is a command of +4…+9. 2.22.0 changed the shed cap; it could not
   touch this. Its premise was also wrong for this flight: at the 36 deg/s² shed peak (47.4 s) the stabs were symmetric
   (L +10 / R +12); the yaw at the top came from the flaperon drag split (`decel` 0.6–0.8) and the airframe.
3. **The bottom −11.3° (10–14° on four of the six) is the roll stop, not the pitch cap.** At 42° the rudders come back and
   the lateral law stops the 76 deg/s velocity roll in 0.5 s with the yaw lagging the roll (p 57 → 2 at −137 deg/s², r 50 → 31
   at −48): velocity-yaw rate +25 deg/s for 0.4 s = the sideslip, while the AoA goes 31 → 0. The 10:40 log's pushes with 50–60
   deg/s ended at 4–9°, its one with 72 at 12.3°: it scales with the rotation. Lateral side; see "What is left".

## Change
`SweepShedMeasFloor` 0.4 → **1**: the shed cap paces to the **capacity** toward the shed side (the estimator's measured +
committed + headroom − unmet, the no-stab read while the stabs are off the pitch as in 2.22.0), never to less. The measured
shed was the honest pace only once the sweep was running; before it moves the capacity is what the surfaces already commanded
will deliver (`ceCmZs` −80 at the push: the stabs' swing to the brake, credited over the servo horizon) — 18–30 deg/s² at the
push, where the measured read 0. The start on this log's states goes from 0 to −8…−17 of pitch command. Telemetry: `capSwp`
and `capShd` (the two nose-down AoA-rate caps, deg/s, 60 = open; the lower binds) — which cap binds would have saved a round
today.

## Tried and rejected on the bench (3 Nov; `SweepYawK`, the floor, the margin and the read bisected one at a time)
- **`SweepYawK` 1** (so the mid-sweep command stays negative): clean released push 600 km/h **9.2 → 21.3°**, 560 7.5 → 15.5,
  one-fin HELD 560 4.8 → 11.3, the regression set +0.3…+2°; 0.65 already 13.3 / 10.1. Tick by tick (released 600): the two
  builds are identical through 55° (the TVC pinned either way), then the larger pitch demand (−48 against a plant at −39) is
  served with the stabs the moment they return to pitch, the lateral axes lose their differential (`unmetX` −94 vs −52), the
  velocity roll stops coordinating (velocity-yaw rate +1 → +17), the capacity read collapses to 0 and the cap goes to +11 —
  the hitch, lower down, with twice the sideslip. **The measured-shed pace was keeping the pitch demand near what the axis
  delivered, by accident; the half-capacity kinematic cap does the same on purpose. 0.5 stays.**
- The caps on the trade's low-passed read (`capTrZeP/N`): released 600 9.2 → 10.6, push-then-pullthrough 450 6.3 → 7.3.
- The crossing allowance less the sideslip already in the drift's direction (hold the nose at 20° when the margin is spent):
  nothing on the clean cases, one-fin released 600 6.3 → 7.7. The bottom sideslip is made by the roll stop, not by the yaw
  left at the crossing.
- Deleting the 2.22.0 no-stab read: clean released 560 7.5 → 11.0 on the bench (its leaf's stabs are the yaw source where the
  game's were symmetric); kept — in the game it only lowers the starting pace (no-stab capacity 19–29 vs 27–44).
- A lead on the kinematic cap (evaluate cos α where the sweep will be in 0.3 s, as `SweepCrossLead`): on the game's states it
  smooths the first 0.3 s and still ends at +7 at 48–45°. Not built.
- `SweepYawK` 1 with everything else as shipped is a one-line A/B for the bench (`bench.sh suite k1 SweepYawK=1`).

## Bench
- Suite vs 2.22.0: reversals, corkscrew entries, regression set, utilisation (4 FAIL / 9) **byte-identical**; damage suite
  identical but one line (clean released 560: same 7.5° peak, reached at 6.0 s instead of 2.2). The bench's leaf sheds 2× the
  game's, so its measured shed was never under the capacity there — the change does not exist on the bench's scripted pushes.
- Replays of the six 11:19 pushes from their logged states (bench: sideslip ~2×, leaf yaw ~½): the first 0.3 s of command
  −8…−27 against 0…+4 on 2.22.0 (all five fast ones); mid-sweep command maxima −2.5 / +1.7 / −4.6 / +6.2 / −8.2 against −6 /
  +5.3 / −5.9 / +16.6 / −8.3; body pitch rate mid-sweep maxima −4 / −4 / −13 / +2 / −12 against −8 / 0 / −18 / +12 / −16.
  Top sideslip unchanged (±0.1). **Bottom sideslip on the bench: −15.6 → −17.9, 7.0 → 5.7, −15.1 → −14.5, 6.6 → 14.1, −10.8 →
  −15.5** — on three of six the nose no longer pauses at 30–45° while the bench's roll stop happens, and the stop's sideslip
  lands at lower AoA. The pause was the hitch; the stop is the lateral law's (below). Flight decides the game's figure.

## What is left, and whose it is
- With the rotation unbraked at 76 deg/s the kinematic cap at half the capacity allows 13–18 deg/s of AoA rate at 50–40°
  against +20 of flight-path rotation: **the nose still comes to about zero body pitch rate at 50–40° and goes on once the
  roll brakes at ~42°** — now from a −17…−23 start without the 0.25 s wait, so the brake from the overshoot is smaller (bench
  replays above), not gone. It goes when the velocity roll is braked earlier or the stop is coordinated; the bench says the
  pitch side cannot buy it with `SweepYawK`.
- **The roll stop at 40–30° is yaw-led** (roll braked at 3× the yaw's rate → 10–14° at the bottom with 70–80 deg/s on; the
  bench doubles it when the nose does not pause). That is the lateral governor's §4 (a)/(b): the stop of a released velocity
  roll through the fade paced so the roll follows the yaw down (the 2.16.0 coordination floor's rule, which today applies only
  with the stick on the rotation's side). `PushoverSideslip` is not that knob.

## What to fly
Released-roll push-overs from 65° at 180–250 kt with 60–80 deg/s on, and one or two with 35–50. Pass: the pitch command and
the nozzles move the instant the stick goes forward (no 0.25 s at zero); the body pitch rate eases from its first value toward
zero at 50–40° without the +13 bump; `capSwp` / `capShd` in the CSV say which cap bound. Expect the bottom sideslip (10–14°
with 70–80 deg/s on) to be the same or a little more until the stop is paced; that is the next build. `LogOutput.log` must
say 2.23.0; the CSV has 303 columns.
