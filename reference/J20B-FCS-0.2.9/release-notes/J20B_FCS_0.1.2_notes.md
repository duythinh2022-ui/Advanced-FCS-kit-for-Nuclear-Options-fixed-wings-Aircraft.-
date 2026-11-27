# J-20B Weilong FCS 0.1.2: negative AoA hold, AoA hold in rolls at the limit

Base: 0.1.1. **Status:** compiled against the game's assemblies, benched. **Not flown.**

## Tom (24 Nov)
- 02:17: "check log, the jet hold negative aoa horribly, rolling at max aoa is not very good, when roll and with full stick pitch, aoa struggles to hold at max, often undershoots to low 30s. Fix the fcs and the airframe"
- 02:29: "i think x1.5 canards is too much, find a different way, and improve roll rate with canards pitch only, maybe let them carry the pitch more and let the trailing flaps do roll"
- 02:41: "maybe keep roll rate the same but focus on the aoa and pitch fix"

## What was wrong
Every stabilator's pitch moment is planned on a one-sided monotone envelope (shared code). That envelope holds the lift peak on one side of the travel and flattens the other side to the value at the end of the travel. On the J-20B's canards (+45 LE down / -25 LE up, foil peak at 36.6 deg local AoA) both sides cause trouble:

- **Push side (LE down).** The end of the 45 deg travel sits deep in stall, so the curve was flattened to that value from about 16 deg of local AoA on. At -20 deg AoA the FCS believed the canard's best nose-down was -28 deg/s^2; the real peak is about -37. It asked the elevons for the difference, which they don't have at negative AoA (about 9 deg/s^2 each pair). The result was oscillation, and overshoot when braking.
- **Pull side (LE up).** Past the peak the curve was held flat, so in a full roll at 40 deg AoA, with pitch saturated, the allocator drove the canards 11-25 deg LE up into deep stall. It believed that cost nothing; in fact the nose-up fell away. Bench, 400 km/h: AoA 39 -> 30 within 1 s of the roll.
- **Settled window too slow** (found once the real curve was on). In a full push the AoA runs at 35-40 deg/s. The settled window walked at 30 deg/s and fell behind, so the canards rode past their peak, where their real gradient is reversed. At 300 km/h with the real curve and a 30 deg/s walk, the push overshot to -35.7 against the 30 limit.

## Changed
| Setting | 0.1.1 | 0.1.2 |
|---|---|---|
| `CanardTruePitchCurve` (new, 4. Gains) | - | **on**: the canards are planned on their real curve (peak and post-stall drop included) at all times |
| `SettledWalkRate` | 30 | **60** deg/s |
| `CanardLiftScale` (new, 5. Airframe) | - | 1 (the mod's) |
| `ElevonLiftScale` (new, 5. Airframe) | - | 1 (the mod's) |
| `CanardMonotonicLift` / `CanardSmoothStartAoA` (new, 5. Airframe) | - | off / 11 |

- **Your existing dev config:** `J20ConfigRevision` goes 1 -> 2. `SettledWalkRate` is moved once, and only if it is still exactly 30. The log line is `J-20B FCS 0.1.2 defaults: ...`.
- **Shared code:** `FcsController` gains one static, `HonestCanardAlways` (default 0, so other jets are unchanged). The J-20B's Plugin sets it from `CanardTruePitchCurve`. Port the static to the other jets' copies on the next shared sync.
- **No airframe change in the defaults.** The new airframe keys are left at the mod's values (see "Tried and dropped").
- **The canards still roll** (`CanardSymmetricOnly` stays off), so roll rate is as in 0.1.1.

## Bench (0.1.1 vs 0.1.2)
3000 m, mass 23 t, CG 0.6 m ahead of the mains.

**Full push** (throttle 0.5, full forward stick from 0.2 s, 6 s, limit -30). AoA after 2.5 s, min / max / mean:

| IAS (km/h) | 0.1.1 | 0.1.2 |
|---|---|---|
| 200 | -29 / -2 / -18 | -29 / -17 / -26 |
| 225 | -29 / -3 / -17 | -28 / -14 / -22 |
| 250 | -28 / -4 / -15 | -29 / -19 / -27 |
| 275 | -30 / +1 / -13 | -28 / -15 / -20 |
| 300 | -26 / -3 / -11 | -27 / -9 / -16 |
| 350 | -29 / -7 / -17 | -27 / -9 / -19 |
| mean | -15.3 | -21.6 |

Neither version passes -30 by more than 0.3 deg here; the 0.1.1 problem on the bench is the AoA falling away mid-push (max column), not overshoot. At 400 km/h both versions are held by the -3 g limit (about -13 to -16 deg).

**Full aft + full roll at 3.1 s** (throttle 1, 7 s, limit 40). Roll angle in 3.9 s, AoA min / max during the roll:

| IAS (km/h) | 0.1.1 | 0.1.2 |
|---|---|---|
| 250 | 71, 34.3 / 40.0 | 58, 39.4 / 40.6 |
| 325 | 65, 34.6 / 40.4 | 70, 39.1 / 40.6 |
| 400 | 67, 36.9 / 40.6 | 73, 38.6 / 41.0 |
| 475 | 97, 34.1 / 40.3 | 69, 38.5 / 40.4 |
| 550 | 64, 37.3 / 40.2 | 75, 38.0 / 40.7 |
| 625 | 80, 37.5 / 40.7 | 87, 34.2 / 40.1 |
| mean roll | 74 | 72 |

AoA under 37 deg in the roll: 4 of 6 cases in 0.1.1, 1 of 6 in 0.1.2. The responses are sensitive to the exact state, so a single case can swing ±10-20 deg of roll either way (475 km/h here).

**Unchanged** (0.1.1 -> 0.1.2):
- **Hands-off at 500 km/h:** AoA 1.8-2.7, no roll or sideslip.
- **1 g full roll at 600 km/h:** 160 deg/s peak in both, 156 deg of bank at 1.3 s.
- **Full pull at 900 km/h:** 9.34 g both.
- **Engine-out full pull at 300 km/h:** holds 39.9-40.3, sideslip under 0.4 deg.
- **Elevon use in the steady 40 deg hold, 250-625 km/h:** mean |deflection| 19 vs 20 deg.

## Still open
- **Mid-push AoA bleed at 300-350 km/h.** The push reaches -27..-29, then after about 1 s bleeds to -9..-15 for a while before coming back. Static map at 300 km/h: the best net nose-down at -12..-28 deg AoA is only -19..-23 deg/s^2. The airframe's nose-up at negative AoA (+16..+41 deg/s^2 at -28..-12) nearly matches all the surfaces together, and the elevons give little there.
- **Elevon crossing.** In those bleeds the allocator also crosses the elevons (left inner up / outer down, right the other way) and holds its output still for about 0.3 s. That spends some of the little nose-down there is. Not found yet.
- Not benched: pushes with roll, damage cases, stores.

## Tried and dropped (bench numbers)
- **Canard lift area x1.5** (`CanardLiftScale`).
  - Rolls at the limit held 39.4-41.3 deg AoA.
  - The push side went divergent past about -27 deg, with overshoots to -50..-78.
  - Tom: too much.
- **Straight-line canard lift curve** (`CanardMonotonicLift`). At the mod's canard size the full-roll pulls dipped to 30 deg (400 km/h). The plateau problem it was for is now handled by `CanardTruePitchCurve`.
- **Canard stall moved out to 45-50 deg local AoA.**
  - Nose-up reserve at 40 deg went 16 -> 19 deg/s^2; nose-down at -20..-36 went -19 -> -22.
  - Not plausible for a thin flat-plate canard (Tom) and too small to matter.
- **Pitch-only canards** (`CanardSymmetricOnly`). Roll at 40 deg AoA fell to 34-65 deg in 3.9 s, and the AoA hold didn't improve.
  - At 40 deg a roll around the flight path needs yaw. Elevon roll comes with almost as much adverse yaw: 78 roll vs -72 yaw deg/s^2 in stability axes at full differential. The canted fins' yaw comes with opposing roll (-66 vs +99).
  - Together they net about 30 deg/s^2.
  - Bigger elevons (x1.25, x1.5) didn't raise the roll rate.
- **Canard peak window in rolls** (the retired `CanardAttachedWindow`). It held AoA 38-41 in rolls but cost 8-9 % of roll rate. `CanardTruePitchCurve` gets the same AoA hold without it.
- **StallCost off for the canards.** It helped 300 km/h pushes but made 250 worse. Overall it was within noise of leaving it on.
