# F-22E FCS 2.7.0 — the measured lateral governor

Base: **2.6.12**. Minor bump: a control-law replacement. **Bench-only, not flown.** 2.6.12 is kept as `.dll.2.6.12.bak`.

## What it is
One governor on the commanded velocity-vector roll rate, built from measured state and the allocator's own output — the rebuild the 2 Nov handoff asked for. It replaces, in one device:
- the 2.5.8 rate cap and its rudder-crossing model,
- the `RollPaceRateBlend` blend of the two laws,
- the window's rudder-load gate and rudder-table reach (`PaceLoad0/1`, `PaceRudderUse`, `gPos/gNeg`),
- the open-loop acceleration cap (`paceAccelCap`, the 2.5.3–2.5.7 pacing),
- the window's table-based release pace.

All of those are still in the source behind the switch (A/B), not deleted yet (your rule: retire against a log, not by assertion).

**Each tick, for the yaw axis and for the roll axis:**
1. **Capacity toward the side the stick's demand goes** = the smaller of
   - what the allocator is delivering of the loop's demand: `omegaDotDes` less the allocator's own unmet report (`allocUnmet`, new: target − achievable at the commanded deflections, per axis, signed, 0.05 s filter), and
   - what the axis is measurably doing (filtered ω̇) plus the commanded increment its servos reach within the 0.15 s horizon (the tables evaluated only at the deflection the allocator actually commanded — its decision, not a hypothesis);

   never less than half of what the axis has shown it can do (`aYawSeen`: a 3 s decaying peak of |ω̇|), so a move can start from rest.
2. **Allowed lead** of the roll target over the axis = the lead that capacity closes inside the margin left, L = √(2·a·(margin − |β|)/cos AoA) (roll axis: /sin AoA), never less than a × 0.15 s (so the loop keeps driving the axis at its capacity when the margin is spent instead of pinning the target to it, which stalled the move and limit-cycled on the bench), plus `PaceFloor` scaled down with the margin left.
3. **Margin** = `RollStopSideslip` (your 4, with the probe's cap) less the sideslip the FCS itself made: the share gravity made (∫(g/V·sinφ·cosθ − KBeta·βg)) is excluded, but only as far as it exists in the measured sideslip and has its sign — gravity is not blamed for sideslip the yaw loop is already correcting. The one acceptable failure, per your spec.
4. **The two windows intersect.** When they don't overlap, the laggard stands: the axis whose implied velocity roll is further from the stick's demand is the one that can't keep up, and the other axis is brought back to it (a yaw that can't follow holds the roll down to it; a roll that can't follow holds the yaw back to it).
5. **The trajectory's acceleration / braking is never touched.** `PaceRevIntent`: a reversal the stick asked for brakes at the reversal's tuned acceleration even while the window holds the target on the near side — not at the stop's.

Nothing binds while the axes keep up. No rudder load, no surface list, no hypothetical reversal, no blend, no AoA schedule. The only lever is the commanded rate.

## Player config (section 6)
- **`MeasuredGovernor`** (new, default **true**; takes effect at once). **Off** = exactly 2.6.12 (verified on the bench: identical numbers), and only then do `RollPaceRateBlend` and `RateCapReversalOnly` apply.
- Your other keys (`RollStopSideslip` 4, `PullRollTrade` 0.7, `RollPaceRateSlip`, `RollGovernorPitch`, `RollStopSlipProbe`) unchanged and still read.
- Dev: `PaceMeasuredStatic`, `PaceSeenUse` 0.5, `PaceSeenTau` 3, `PaceSatTau` 0.05, `PaceRevIntent`, `PaceHorizon` 0.15, `PaceFloor` 4, `PaceMarginK` 1. A sweep of horizon 0.10, floor 2 and seen-share 0.25 moved single cases by ±1° and nothing systematically; the defaults stand.

## Not replaced in this build (still the old devices)
- The **coupling roll cap** (`coupRollLim`, inertial pitch-up): table version, unchanged.
- The **pitch side of the trade** (`RollGovernorPitch`: AoA-rise cap from the rudders' braking yaw and load; the sweep / shed caps from `aYaw` with the `yawSat` derate): unchanged. This is the next stage, and it is where the one bench regression below lives.

## Bench (calibrated bench; reads sideslip about 2× high; margin 4)
**Utilisation invariant** (full stick, sideslip well inside the margin → command must sit at its cap): **1 FAIL / 9** (a15-25 ias75-95: 63 %) against 2.6.11's 6 FAIL / 10. No steady-state tax anywhere else: falling leaf 96–99 %, 40–55° 100 %, 25–40° 98 %.

**Corkscrew entries** (full lateral + pull from 1 g; early peak / dip / mean, deg/s):

| entry | 2.6.11 | 2.7.0 |
|---|---|---|
| 556 km/h full pull | 73 / 37 / 63 | 85 / **69** / 71 |
| 630 km/h full pull | 96 / 63 / 77 | 127 / **96** / 102 |
| 700 km/h full pull | 121 / 90 / 100 | 130 / **132** / 140 |
| 850 km/h full pull | 247 / 104 / 131 | 223 / **132** / 147 |
| 556 / 630 / 700 km/h, 0.7 pull | 85 / 128 / 136 dip | 128 / 136 / 159 dip |
| 400 km/h full pull | 41 / 21 / 44 | 43 / 8 / 40 (worse: the low-speed entry dips to the yaw's level before climbing) |
| 400–700 km/h, T/W 0.22 (low thrust) | dip 13 / 35 / 38 / 31 | dip 8 / 60 / 67 / 82 |

**Reversals** (time to zero velocity roll / peak sideslip; 2.6.11 numbers are its blend 0.41):

| reversal | 2.6.11 | 2.7.0 |
|---|---|---|
| pull + roll, 450 / 556 / 650 km/h, 11–13° | 0.23 / 0.23 / 0.28 s; 5.8 / 6.5 / 3.9° | 0.38 / 0.35 / 0.35 s; 8.0 / 4.4 / 4.1° |
| half pull, 400 km/h, 29° (from 99 vs 63 deg/s) | 0.25 s, 5.5° | **1.55 s**, 5.9° |
| half pull, 556 km/h, 20° (from 171 vs 146) | 0.38 s, 5.8° | **1.45 s**, 4.8° |
| half pull, 700 km/h, 11° | 0.33 s, 4.1° | 0.43 s, 3.7° |
| full aft, 300 km/h, 63° | 1.53 s, 5.4° | 0.92 s, 4.1° |
| full aft, 500 km/h, 49° | 0.40 s, 6.1° | 0.55 s, 6.3° |
| falling leaf, 160 / 300 km/h | 2.28 / 1.78 s; 7.7 / 6.3°; new peak 47 / 50 | 1.03 / 1.67 s; 9.5 / 5.8°; new peak 41 / 50 |
| 1 g, 500 / 800 km/h | 0.35 / 0.38 s | same |

The moderate-AoA reversals are **paced by the bench's yaw**: in the 400 km/h / 29° case the bench yaw brakes at 20–35 deg/s² with the rudders crossing then pinned, and the window follows it. In your 01:08 log the game's yaw braked at ~110 deg/s² in the comparable 289 kt / 25° reversal. The governor is measured, so in the game it will pace to the game's yaw — the bench absolute here is the bench's pessimism, not a design number. That is the thing to read in the first log (below).

**Regression set** (13 scenarios, worst sideslip): most within ±1° of 2.6.11, roll rate up in all. **Worse:** push + roll 200 / 300 km/h 3.4 / 2.6 → 6.3 / 6.4°; push-then-pull-through + roll 450 km/h 4.2 → 8.4°; pull-through with the roll released at zero 3.7 → 8.0°; pull-then-roll 250 km/h (46°) 4.6 → 7.4°. This is the zero-crossing family the architecture handoff flagged: the sideslip there comes from the yaw rate left over as the AoA sweeps through zero, which no roll target can affect (the roll's yaw share pS·sin AoA is ~0 there). The old rate cap "earned its keep" there by trimming the steady roll beforehand — the pre-emptive cut you reject. The honest lever is the pitch side (slow the AoA sweep from measured yaw capacity), not built yet.

## What to fly, and what the log will show
`MeasuredGovernor` on: the 340 kt corkscrew entry; reversals at 15–45° from a held pull at 250–300 kt; falling-leaf reversals at 160 / 300 km/h; push-over corkscrews through zero AoA. Then off, repeat. New log columns: `paceGateZ/X` (1 = window active), `unmetX/Z` (the allocator's shortfall, deg/s²), `capZP/capZN/capXP/capXN` (the capacities), `paceMLeft` (margin left after gravity's share), `paceRH/pacePH` (where the axes will be in 0.15 s), `aYawSeen`, `pSRaw` (the stick's target before the governor), `betaG2` (gravity's sideslip estimate). The validation the handoff asked for: in each reversal, `capZN`/`capZP` against the yaw acceleration the jet then actually made (`r` differentiated) — within about 1.5× means the estimate is honest; the window's hold (`pSRaw − pSTarget`) against the sideslip (`beta`) says whether it is spending the margin or hoarding it.

## Environment notes
`bench/an/revsuite.py`, `corkentry.py`, `reg.py`, `utilcheck.py` run from `/home/claude/work/suite.sh <tag> [Name=value…]`. `regen.sh` fixed to find `tomcfg.cfg` (it looked in `bench/`). The effector loop in the governor must read `eff.cmd` (the allocator's decision); `cmdOut` is the servo trajectory and is always within a tick of `now`.
