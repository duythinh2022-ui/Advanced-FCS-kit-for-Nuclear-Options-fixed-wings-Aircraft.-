# F-22E FCS 2.30.0 — the air data at the CG, not at the nose rigidbody

Base: **2.27.0** (the installed, best-flown push-over build; 2.28.0/2.29.0 stay rejected). **Compiled + bench-checked (full suite
on the leaf-calibrated bench, same metric for both builds) + replayed, not flown.** No tuning value touched, no player or Dev keys.
CSV 306 → **309 columns** (`alphaRoot, betaRoot, airRefCg` at the end of the FCS block). 2.27.0 kept as `.dll.2.27.0.bak`.

## What the bench calibration found (`claude/Bench_leaf_calibration_results.md` §2)
`ac.rb` is the **root part's** rigidbody, and its CoM sits **2.48 m ahead of the aircraft CG**. Every FCS build so far took its
AoA, sideslip, TAS, flight-path rate and speed rate from that body's velocity, which carries ω × 2.48 m: q·2.48/V of extra AoA and
r·2.48/V of extra sideslip. In a 60–75 deg/s rotation at 130–200 kt that is **1.2–2.0° of sideslip** the yaw loop, the capacity
read, the pace and the trade were working from, and up to ~1° of AoA in a fast pitch. The governor's kinematics
(β̇ = p sin α − r cos α + g/V…, pS = p cos α + r sin α, the flight-path rotation) are CG equations, so they were mixed with a nose
measurement. The logs had the same reference, which is what made the bench read "2× the game's sideslip" since 2.7.x.

## The change (one, inherent; `FcsController`)
- `vCg` = the mass-weighted velocity of every rigidbody of the jet (the same weighting the FCS already used for the CG position).
- **AoA, sideslip, TAS / IAS** from `vCg` (`AirDataAtCg`, static, true; false = 2.27.0 byte for byte — checked on a replay: md5
  identical).
- **Flight-path rate `omegaV` and speed rate `vDot`** from the CG's acceleration (`FlightPathAtCg`, static, true). Bisected: no
  effect on the replayed pushes; it is in for consistency of the kinematics.
- The pitch model's slopes (`ComputeHybridSlopes`, whose own comment says "the CG's air velocity") and the drag direction use
  `vCg`. Unchanged on purpose: **`nz` stays the root body's** (the G loop's feel at high IAS — a separate change if wanted), and the
  model copies of the mod's downwash hook keep the root velocity the hook itself uses in the game (so the FCS's model still matches
  the aero the game applies).
- Telemetry: `alpha`, `beta`, `tas`, `ias` columns are now the CG's; `alphaRoot`, `betaRoot` the old reference; `airRefCg` = 1.
  The FcsApi AoA (HUD / visualizer) is the CG's too. The bench reads `airRefCg` and poses 2.30.0 logs at the CG (older logs keep
  `LogRefRoot=1`).

## Bench (leaf-calibrated bench from the other chat; both builds scored with the plant CG's sideslip so the metric is the same)
Diffs in this folder (`suite_v230_vs_v227_*.diff`). Sideslip in degrees.
- **Clean push-overs from 65°:** peak 7.2 → 6.6 (560 km/h), 7.6 → 7.3 (600); after the push 4.3 / 4.4 → 3.1 / 3.3 (released and held).
- **Clean leaf stops / reversals:** 296 km/h 5.0 → 4.3, 370 km/h 7.2 → 5.0 / 7.3 → 5.0.
- **Reversals:** leaf 160 / 300 km/h 9.8 → 8.5, 7.1 → 5.3; full-aft 300 / 500 6.0 → 4.7, 10.2 → 9.4; 0.5-pull 400 9.0 → 8.5
  (t90 1.27 → 1.20 s), 556 new-side overshoot 234 → 206 deg/s (t90 1.17 → 1.08). **Worse: pull+roll reversal at 0.7, 450 km/h
  16.8 → 20.2** (t90 1.70 → 1.58 s — the reversal is faster there and pays in sideslip); 556 / 650 unchanged / 22.4 → 21.7.
- **Corkscrew entries:** AoA overshoot 0.3–1.0° lower in every one of the 20 cases; sideslip lower in 16 (by 0.2–1.4), higher in two
  (thr 0.3 556: 8.8 → 10.4; pull 0.7 556: 8.4 → 10.1); roll dips ±3 deg/s.
- **Regression set:** 65° releases 4.6 / 4.7 → 3.7 / 3.8 (the rate-cap hold now reads 14 % at the peak there, was 3 %);
  push-then-pullthrough 450 8.2 → 6.3; pull-then-roll 320 8.2 → 6.6; **worse: full-aft + roll + reversal 320 6.0 → 6.8, 500
  5.3 → 7.1.**
- **Damage suite:** one-fin held push 560 10.9 → 6.0; no-fin pushes 560 18.2 → 16.6 but **600 released 16.1 → 20.1, held 11.6 → 15.3**;
  one-fin leaf reversal 370 2.9 → 5.4; fins + R flaperon reversal 36.9 → 39.6 (departs on both). The bench's no-fin plant is the
  least calibrated part of it.
- Utilisation invariant: identical (4 FAIL / 9, as since 2.9.0).
- **Replay check on the 2.27.0 log (14:45):** with the 2.27.0 law the plant still fails the body-pitch criterion on 8 of 9 pushes
  (the calibration's open item: the bench under-predicts nose-down pitch acceleration in the first second of a push by 7–9 deg/s²).
  **So the push-over feel of 2.30.0 is not judged by the bench**, only its sideslip. On the replayed held push at 109.16 s the
  command's mid-sweep ease starts a little earlier (−13 vs −18 deg/s at 52°) and recovers sooner (−9 vs −4 at 31°); the bottom
  sideslip is 1.5–2° lower (+3.4 vs +5.1 at 20°).

## What to expect in flight
On the bench the jet holds sideslip a little tighter in rotations at high yaw rate (corkscrews, leaf, the top of push-overs)
with the roll rates within ±3 deg/s of 2.27.0; push-overs about as 2.27.0 (the law is 2.27.0's). The HUD's AoA/sideslip read the CG now: in a fast rotation they
will differ from what you are used to by ~1–2° of sideslip — the margins (4°) keep their values but are now measured at the CG,
which is where the governor's equations assume they are. Watch: the pull+roll reversal at ~450 km/h (bench +3.4°), the full-aft
roll reversals, and push-overs without fins. `LogOutput.log` must say 2.30.0; 309 columns.
