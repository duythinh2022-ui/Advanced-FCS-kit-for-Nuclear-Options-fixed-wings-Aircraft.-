# F-22E FCS 2.7.3 — the pitch side of the trade on the same measured capacity

Base: 2.7.2. **Bench-only, not flown.** 2.7.2 kept as `.dll.2.7.2.bak`.

## What the 11:11 log showed (2.7.2)
Tom: *"for corkscrews at higher airspeed it feels like there is a delay in pitch, like roll starts first … a pitch hitch, especially when I command roll at high AoA, then release, then pull … pitch goes a bit, stops, then goes full send … velocity-vector roll rate at the high end of the mid-range AoA is slow until max AoA is reached, then it feels like it spiked."*

All three are the pitch side of the governor, which 2.7.0–2.7.2 had not converted and which still read **rudder load and rudder braking tables**:
- **362 kt entry (227 s), full pull + full lateral:** the roll target is released at once (−122 deg/s at 227.0); the pitch command `qCmd` goes 58 → 42 → 36 → 33 → **26** and recovers only at 227.6. `yawSatPitch` (the AoA-rise cap of `RollGovernorPitch`) reads 3–5 deg/s of allowed AoA rate at 227.1–227.2 and `tradeAlpha` (the trade's pitch share) 12–15, with the rudders at 40–90 % load (`stopSlipU`). That is the "roll starts first" and the hitch. Same at 345 kt (164 s): `qCmd` 41 → 28 for 0.3 s, `tradeAlpha` 15–16 for the whole pull.
- **"Slow until max AoA, then spikes":** the trade's roll limit `pullTrade` (= `pSmax` 100 → 50 deg/s while the AoA rises 30 → 42°) lets go once the AoA stops rising (its budget was a rudder-load share of `aYaw`), and the roll steps up. The trade's design, with a budget that read low.

## 2.7.3
The three pitch-side devices now read the **same airframe-wide capacity as the roll side** (the pace window's per-side figure from the previous tick: every effector's yaw headroom from the commanded deflections toward the side the sweep needs, less the allocator's unmet, plus the directional-stiffness passive share):
1. **`RollGovernorPitch` AoA-rise cap:** `aLeft` = capacity on the side of the roll's own yaw (sign of pS · sin AoA) less the roll's own build (|pṠ| sin AoA), in place of the rudders' braking table × (1 − rudder load). Its fade-in (`pinGate`) reads the same capacity.
2. **The feed-forward trade's budget:** `PullTradeFreeK` (0.35) × the capacity on the side the asked sweep grows the demand toward (nose-up at positive AoA: the roll's yaw side; nose-down: the other side), in place of a rudder-load share of `aYaw`. Floor unchanged.
3. **Sweep / shed caps** (push-overs, pull-through-zero): the larger side's capacity in place of `aYaw` × (1 − `yawSat` derate).
`MeasuredGovernor` off = all three as in 2.6.12.

## Bench (2× pessimistic on sideslip)
630 km/h full-pull entry, 2.7.2 → 2.7.3: roll dip 100 → 69 deg/s at 0.8 s → **none** (93 → 96 → 100); pitch command dip 59 → 21 deg/s, back to 36 at 1.0 s → 59 → 24, back to 37 at 0.8 s. Entries 400–850 km/h: roll dips 22–197 (2.7.2: 25–190), sideslip +0.5° on average. Reversals: low AoA 0.22 s (same), full aft 500 km/h 1.00 s / 7.9° (2.7.2: 0.70 / 6.7 — from 81 deg/s instead of 57: the trade holds less roll during the pull); pull + roll reversals 8.6 / 9.3° (2.7.2: 6.6 / 5.5, from 63 / 80 instead of 60 / 71) — the price of the trade giving the pull more and the roll more at once. Utilisation 5 FAIL / 10 (40–55° AoA cells improved to 75–80 %). Falling leaf unchanged.

**What remains of the entry pitch cut** is the roll's own yaw build spending the capacity in the first 0.3–0.4 s: a 500 deg/s² roll entry at 15° AoA asks the yaw for ~125 deg/s² of the ~200 available, and the pull is held to the rest. That is the pitch cap doing what it is for; if you want the pull to win that first 0.3 s, `PullRollTrade` is the knob (it decides who yields).

## What to fly
Entries at 300–400 kt (the pitch command through the first 0.5 s: `qCmd`, `yawSatPitch`, `tradeAlpha`); roll at high AoA → release → pull after a moment; corkscrews through 30–45° AoA (`pullTrade` against `pSTarget` as the AoA tops out).
