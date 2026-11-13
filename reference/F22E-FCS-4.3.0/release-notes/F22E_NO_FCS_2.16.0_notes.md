# F-22E FCS 2.16.0 — the rotation the jet has is followed, not cut; the sweep cap is per axis; the estimator's yaw read is honest

Base: 2.15.0. **Compiled + bench-checked (clean-jet regression set: sideslip 0.1–4.4° lower on the seven push-over cases,
+0.1 s to zero AoA on three of them and +0.66 s on the 108 kt push-with-roll; reversals, corkscrew entries within noise;
damage suite: one-fin push-overs 4.6–12.3° → 2.4–5.8°, fins + flaperon reversal 31 → 23°, no-fin push-overs still depart on
the bench), not flown.** 2.15.0 kept as `.dll.2.15.0.bak`. No tuning values touched. New statics, all on, for the bench A/B:
`PaceRateCapEst`, `LatLimitYawFloor` (+ `LatFloorSinMin` 0.26), `SweepPerAxis`. Nothing deleted this round (see the end).

## What the 20:19 flight (2.15.0, both fins off) and the damage bench showed
The leaf at 65° without fins is fine on 2.15.0: rotation 35–40 deg/s, reversals cross zero in 1.8–2.5 s, |β| ≤ 4–5°, the stops
start at once (46 → 35 deg/s in the first second) — the 2.15.0 stab change did its job. **The held-roll push-over from 62° at
205 kt still departs** (120 s: β 35° in 2 s, then a 25 s tumble; 162 s the same at 165 kt). The chain, tick by tick, and the
bench twin (`dmgsuite` 'pushover HELD 560', the same chain):

1. The yaw (−47…−63 deg/s) sheds on its own at 15–23 deg/s² (the stabs' and flaperons' yaw at high AoA) — the pitch was paced
   to roughly that (AoA 62 → 45° at ~15 deg/s with ~10–15 of shed-side capacity). The pitch side was not the failure.
2. As the AoA crossed the rudder fade (59 → 49°, `rudderAuth` 0 → 1) the lateral limit's fin-health factor took **pSmax 61 → 16,
   rMax 54 → 11**. The roll trajectory braked at the tuned rate (the 2.7.1 pace rate limit does not slow a falling *demand*):
   **p −28 → −13 while the yaw of −47 needed p = r / tan α = −42** to stay coordinated. β̇ = p sin α − r cos α ≈ +20 deg/s.
   Departure. A rate *limit* applied to a rotation that already exists does not slow the yaw (nothing can, without fins);
   it only takes the roll away from it.
3. Deleting the fin-health factor outright (tried first) made every damaged bench case worse (one-fin push-overs 5–9° → 12–25°,
   fins + flaperon 31 → 52°): as a *steady-rate* limit at low AoA, where the yaw is commandable, it is doing real work until
   the rate cap carries the fin loss. Kept.
4. The 2.15.0 limiting-axis estimator read went **negative on the shed side** (−8…−27 at 120.7–121.3 s) while the yaw was
   measurably shedding 15–23: the roll axis's deceleration (the roll being cut) was read as a yaw shortfall.
5. Once the roll was kept with the yaw (bench), the next thing that let the sideslip run was the sweep cap: it summed
   SweepRollShare × the roll authority into the yaw term and read the larger yaw side, so it allowed 36 deg/s of AoA rate where
   the shed side's 10 deg/s² supports ~10 (the yaw a coordinated rotation carries, pS sin α, must fall at pS cos α × AoA rate).

## Changes
**Lateral limit — coordination floor** (`LatLimitYawFloor`): with the stick on the rotation's side, the velocity-roll limit
(`num10`, before the stick scales it) is floored at the roll the body yaw implies, r / sin α (sin α floored at 0.26 so a
low-AoA turn's yaw is not read as a rotation). The limit still bounds what the stick may *build*; it no longer brakes the roll
away from a yaw it cannot shed — the roll follows the yaw down as the yaw sheds. The stick scales the target as always; a
release is the window's job as before. Telemetry: `latFloor` (how much the floor raised the limit, deg/s). Clean jet: inert
(the yaw a clean rotation carries never exceeds the clean limit) — regression set / reversals / entries within noise.

**Sweep cap per axis** (`SweepPerAxis`): `capSweep` = SweepYawK × min(yaw capacity on the sweep's side / (pS cos α), roll
authority / (pS sin α)) in deg/s of AoA rate. A sweep toward zero takes yaw away (the shed side, opposite the rotation); a
sweep away from zero adds it (the rotation's side). The 2.1.8 sum (larger side + 0.5 × roll authority over pS) is the `false`
branch. **This is the one change the clean jet feels**: bench push-overs run 0.1 s longer to zero AoA at 190–240 kt (sideslip
4.2 → 3.7, 7.3 → 6.9, 6.8 → 5.2, 7.5 → 6.1°) and 0.66 s longer at 108 kt with a roll held (5.4 → 1.0°). If the released push
from 65° at 210–250 kt feels slower than 2.10.0's "where I like it", that is this device — say so and I take the clean-jet
side of it back out; the fin-loss side is right.

**Estimator — the yaw read of the two-axis probe**: the probe is still sent along ±u (cos α, 0, sin α) so the roll surfaces
stay on the roll, but its answer is read on the **yaw axis alone** (the Z increment per group, the measured and committed yaw,
the yaw unmet — exactly as `capZP/N` read the yaw-only probe). 2.14.0 summed the axes (credited the flaperons' roll surplus),
2.15.0 took the limiting axis (read the roll's deceleration as a yaw shortfall). `ceCapSP/SN` keep their columns with the new
meaning. With the rudders present (`rudW` 1) nothing changes.

**Pace rate limit on the estimator** (`PaceRateCapEst`): the 2.7.1 "target moves toward the demand no faster than the yaw can
follow" reads `capEst.capZeP/N` instead of the window model's table headroom (which over-reads the stabs' yaw 3–4× past the
fade). Clean suite unchanged by it; fins + flaperon reversal 31 → 23°. The window's own reach/lead still runs on its 2.7.x model
(tried on the estimator: it holds the 15–55° bands at 55–80 % of capacity — the §4 (b) design, not a substitution).

## Bench (sideslip ~2× high with fins; uncalibrated without — read mechanisms)
Regression set (2.15.0 → 2.16.0): push+roll 200 5.4 → 1.0, push+roll 300 3.2 → 2.4, pull-then-pushover 350 4.2 → 3.7, 450 7.3 →
6.9, push-then-pullthrough 350 4.4 → 4.4, 450 6.8 → 5.2, pullthrough released 7.5 → 6.1, fullaft+roll+rev3s 320 5.1 → 5.8, the
rest ±0.1. Reversals / corkscrew entries: within ±0.3. Damage suite: one-fin push-overs 4.6 / 9.0 / 7.6 / 5.9 → 2.4 / 4.7 / 5.8
/ 4.6; fins + R flaperon reversal at 480 kt 31.4 → 23.0; one-fin leaf stops/reversals, wing cases, engine case unchanged;
**no-fin push-overs 43–64 → 56–75, still departing** — the bench plant without fins sheds 3 deg/s² of yaw at the 65° stop where
the flight sheds 11–23, so its no-fin push is not the flight's; the flight is the test.

**Correction to the 2.15.0 notes:** "utilisation invariant: all PASS" was read off the last three bands only. The check has had
**4 FAIL / 9** (the 15–55° bands held at 54–76 % of a capacity the airframe meets) since at least 2.9.0 — these are the rate cap /
window items of the handoff §4 (a)/(b), unchanged by 2.15.0 and 2.16.0; the 55–70° bands pass at 96–99 %.

## For the next log
Both fins off (~200 kt, `FinLossAoACap` 65): **the held-roll push-over from 60–65°** (the 120 s case) — expect the roll to stay
with the yaw (`latFloor` > 0 through the fade, p ≈ r / tan α) and the nose to wait where the shed is small; the released push;
the leaf as before (should be unchanged). Clean jet: a released push from 65° at 210–250 kt with 60–75 deg/s on, to check the
pace against 2.10.0's.

## Not deleted, and why
`DamageLatLimit`'s fin-health leg and `FinLossAoACap` were on the list for after this flight. The bench says the fin-health leg
still carries the one-fin cases below the fade (the rate cap does not yet); `FinLossAoACap` (yours at 65 = off) stays until a
no-fin push-over from 65° is shown recoverable in flight — if it is not, 40 is the honest airframe number and the key is the
right place for it.
