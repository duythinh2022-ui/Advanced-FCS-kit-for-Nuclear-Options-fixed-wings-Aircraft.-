# Vortex FCS 0.4.10: the g dip when the airbrake opens

18 Nov 2026. Built on 0.4.9 (0.4.6–0.4.9 from the other chat, the STOVL and HUD work, carried unchanged). Speed brake only: the STOVL law is not touched. **Benched, not flown.**

Tom, 17 Nov 23:13, on the 0.4.5 note "g still drops to about 0.7 for 0.6 s as the brake opens above 900 km/h": "fix this".

## 1. Cause

Opening the brake in flight does two things on this jet, and both cost or move lift.

- **The surface set sheds lift.** The rudders flare; on the canted fins that lifts the tail (nose down). The elevons go trailing edge up to hold the nose, and they shed more lift than the flare adds because they work on a shorter arm. Bench, 1058 km/h: **−0.66 g**.
- **The panel gives some back.** The game applies the panel's drag at the panel part's own centre of mass, which sits above the jet's CG: a nose-up moment. The elevons then need less trailing edge up. Same condition: **+0.11 g**.
- **Net −0.55 g**, and the pitch law did nothing about it. With the stick released it holds pitch *rate* (zero), not g or AoA (angle of attack), so the lift only came back as the jet sank and the AoA rose by itself, about 0.4°.

A lift-neutral set is not possible here: both elevon pairs have nearly the same arm, so no split between them changes the set's lift (tried, it ran the surfaces to their limits).

The panel was not in the FCS model at all. Its nose-up moment was also an unknown disturbance for the pitch loop: panel alone on the bench gave a 1.17 g bump opening and a 0.84 g dip closing.

## 2. Change

One Dev switch, `AirbrakeLiftCompensation` (section `7. Airbrake`, default on; off = 0.4.9 exactly). No schedule and no tuned gain: every size comes from force models the FCS already had.

1. **Pitch feed-forward of the lift change.** The FCS works out the lift the full set sheds (from the same surface force tables the allocator uses) and the lift the open panel gives back (from the game's airbrake drag model, already used for `AirbrakeAoAFade`). It converts that to the AoA that replaces it and flies that AoA in as the brake opens and out as it closes. Nose movement: about +0.4° opening, back on closing.
2. **Where it enters.** On the pitch rate the loop tracks, after the stick's command shaping. It is not a stick command: sent through the shaping, a brake tap gave a 1.15–1.25 g bump because the released-stick logic held the reversal back 0.25 s.
3. **The panel's pitch moment is given to the pitch loop** (INDI, the incremental inversion loop) as a known change, the same way AoA and pitch-rate changes already are.
4. **The set moves on an S-curve** instead of a straight ramp cut off at the flare cap, so the pitch rate can follow it. Same 1 s travel; speed lost after 1 s differs by 2 km/h.
5. **Scope.** CTOL laws only. Off under the STOVL law, on the wheels, and when the FCS is off.

Model check, bench, set and panel fully out, lift loss measured against a clean lift fit:

| Speed | Measured | Model |
|---|---|---|
| 1058 km/h | 0.401° (0.55 g) | 0.380° |
| 978 km/h | 0.401° | 0.380° |
| 910 km/h | 0.378° | 0.358° |
| 803 km/h | 0.376° | 0.359° |

The model is about 5 % under.

## 3. Bench results

Tom's config. Stick released, brake opened and closed 3.4 s later. "0.4.9" below stands for 0.4.6–0.4.9: those builds change the STOVL law, the HUD and the trailing-edge sign, and in `FcsController.cs` only telemetry columns. The main g table (both power settings), the taps and the switch-off check were run on 0.4.9; the rest on the same change over 0.4.6. **g is the plant's own**, not the FCS's filtered reading the 0.4.5 note used (that one lags about 0.05 s and reads dips shallower).

The bench's drag is 2–4 times the game's, so the main table is flown at full power, which brings the deceleration with the brake out to 0.57 g (game, idle, about 0.3 g; bench, idle, 1.28 g).

Lowest and highest g, full power:

| Speed | Opening, 0.4.9 | Opening, 0.4.10 | Closing, 0.4.9 | Closing, 0.4.10 |
|---|---|---|---|---|
| 1200 km/h | 0.67–1.01 | **0.88**–1.01 | 0.88–1.13 | 0.88–1.00 |
| 1100 km/h | 0.70–1.01 | **0.90**–1.01 | 0.90–1.15 | 0.91–1.00 |
| 900 km/h | 0.76–1.02 | **0.93**–1.02 | 0.94–1.16 | 0.95–1.01 |
| 700 km/h | 0.85–1.03 | **0.95**–1.03 | 0.95–1.21 | 0.94–1.05 |
| 500 km/h | 0.94–1.02 | 1.01–1.05 | 1.00–1.15 | 0.92–1.03 |
| 300 km/h | 1.02–1.17 | 1.02–1.17 | 0.96–1.03 | 0.95–0.98 |

At idle the picture is the same: opening 0.67–0.69 → 0.88 at 1100–1200 km/h, 0.76 → 0.93 at 900, 0.84 → 0.96 at 700; the closing bump of 1.09–1.12 is gone (0.91–0.99).

Other cases:

| Case | 0.4.9 | 0.4.10 |
|---|---|---|
| One 0.4 s tap, 1100 km/h | 0.70–1.14 | 0.91–1.06 |
| Three taps in 2 s, 1100 km/h | 0.67–1.14 | 0.73–1.06 |
| 9000 m, 600 km/h IAS, idle | 0.86–0.98, then 0.95–1.07 | 0.96–1.00, then 0.96–0.98 |
| 6.5° climb at 1125 km/h, idle (the 21:46 log's condition) | 0.69–0.97, then 0.92–1.11 | 0.88–0.97, then 0.90–0.97 |
| Brake in a banked 4 g pull, 900 km/h | 3.85–4.05, then 3.92–4.19 | 3.92–4.07, then 3.89–4.04 |
| 400 km/h, half power | 0.96–1.09, then 0.92–1.04 | 0.93–1.09, then 0.94–0.99 |
| Panel alone (set off), 1100 km/h | 1.17 bump, 0.84 dip | 1.05, 0.95 |

Unchanged:

- **Switch off:** the same flight as 0.4.9 in all 458 logged columns.
- **0.4.5's brake fix:** `brakecycle.sh` still clean (bank 0.4°, 0 set switches, 0 command reversals, both signs of the roll offset).
- **Manoeuvres with the brake out** (roll, pull and release, pedal at 500 and 900 km/h): at 0.4.9 levels. Lowest g with full pedal 0.80 (900) and 0.91 (500) on both. Command reversals 36 / 93 in the rolls against 26 / 89.
- **CTOL regression suite** (regression set, stop / reversal sweep, corkscrews, damage): line for line the same as 0.4.9. It does not use the brake. 20 "fins + rudder gone" damage rows error out on both, as in 0.4.4 and 0.4.5.
- **STOVL sets:** on the 0.4.6 base all 12 traces identical in every column. Not re-run on 0.4.9: the compensation is off under the STOVL law.

## 4. What is not fixed, and not verified

- **A dip remains: 0.88–0.93 g opening above 900 km/h** (was 0.67–0.76), and 0.88–0.95 closing. Cause: while the set moves, the pitch rate trails its command by 0.25–0.35 deg/s. That is the INDI loop's own delay on moments that change with AoA, plus a few percent of model mismatch for the set. It is in 0.4.9 too, and in the loop all seven jets share; not touched here.
- **Rapid repeated taps** are only a little better (0.68–0.81 lowest above 900 km/h, was 0.61–0.76), for the same reason.
- **The panel's moment arm is from the bench's extract of the airframe.** If the game applies the panel's drag elsewhere, the panel terms (a sixth of the total) are off by that. Not checked against a flight log.
- **Not flown.** The bench's drag is 2–4 times the game's and its plant equals the FCS model apart from the panel.
- **Steady 0.94–0.98 g with the brake held out at high speed** is unchanged: the rate hold runs 0.05–0.1 deg/s low while the jet decelerates.
- **An earlier draft of this fix** (set only, no panel) looked better on the idle bench (0.92 g) only because the bench's excess drag hid a 16 % overshoot: at full power it gave a 1.12 g bump opening and 0.81 closing. Not released.

## 5. Bench changes (in the Source zip)

- The bench FCS now finds the airbrake as it does in the game, and reads its opening (`bench/sim/Airframe.cs`, `Plant.cs`). Before, the bench FCS never saw the panel. A brake run on 0.4.9 is the same flight with or without this; only the two airbrake telemetry columns change.
- `csv=` on a `fly` run gives the plant's own g; use it for g curves.

## 6. Files

`FcsController.cs` (the compensation, the S-curve, the two hooks into the pitch law), `Plugin.cs` (the switch). Native core unchanged since 0.4.5.
