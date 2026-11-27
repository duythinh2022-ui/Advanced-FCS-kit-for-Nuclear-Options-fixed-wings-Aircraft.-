# J-20B Weilong FCS 0.1.4: pitch-only canards by default, 40 deg held within about ±0.5 in rolls

Base: 0.1.3. **Status:** compiled against the game's assemblies, benched. **Not flown.**

## Tom (24 Nov)
- 03:15: "Make canards pitch only as default, and i want 40 fluctuate +-0.5 at most when rolling"
- 03:17: "Also reduce trailing flaps trim cost across the aoa range"

## Changed
| Setting | 0.1.3 | 0.1.4 |
|---|---|---|
| `CanardSymmetricOnly` | off | **on** (pitch only) |
| `ElevonSymCost` (trailing-edge elevons' trim cost, the same at every AoA) | 2 | **0.2** |
| `HighAoARollPitchWeight` (new, 4. Gains) | - | **3** |
| `CanardLiftFloor` (new, 4. Gains) | - | **16** deg |

**What the new settings do:**
- **`HighAoARollPitchWeight`.** It applies at high AoA while rolling (roll stick or pedal in, or the roll still running after a release). The allocator's pitch axis is weighted up by this factor, fully from 35 deg AoA and not at all below 25. So the roll gives way before the AoA does.
- **`CanardLiftFloor`.** It applies when pulling (stick more than 0.3 aft) above 30 deg AoA. The canards aren't sent so far leading edge down that their local AoA drops under 16 deg. Elevons and fin toe take the small nose-down corrections instead. A push opens the floor again.

**Your existing dev config:** `J20ConfigRevision` goes 2 -> 3.
- `CanardSymmetricOnly` is set on once.
- `ElevonSymCost` moves 2 -> 0.2 once, only if it still holds 2.
- The log line is `J-20B FCS 0.1.4 defaults: ...`.

**Shared code:** `FcsController` gains three statics, `HiAoAPitchWeight`, `CanardLiftFloor` and `CanardLiftFloorAoA` (30). Their defaults (1 / 0 / 30) leave the other jets unchanged. The J-20B's Plugin sets them. Port them to the other jets' copies on the next shared sync.

## What caused the AoA swings in rolls (pitch-only canards, 0.1.3)
**Small limit cycle at the AoA limit, 38-40.3 deg, period about 1 s:**
1. The AoA crosses 40 by 0.2-0.3 deg and the capture asks for a little nose-down.
2. At 40 deg AoA the canards sit at their lift peak (local about 37). The curve is flat there and steep below. To get that little nose-down, the allocator sends them about 30 deg leading edge down, to local about 14. That is most of their lift.
3. The nose falls.
4. Coming back, the nose-up is saturated (elevons at their stops, part of them busy rolling), so the AoA undershoots to 38-39.

The same thing happened after a roll release, during the stop.

## Bench, AoA from the FCS's own measurement (what the log records)
Full aft, full roll from 3.1 s, 3000 m. Band = AoA after it first reaches 39.5 following the roll start.

**Rolls, 8 s cases** (roll angle to the end, AoA min / max):

| IAS (km/h) | 0.1.3 settings, pitch-only canards | 0.1.4 |
|---|---|---|
| 200 | 69, 39.1 / 40.4* | 71, 39.5 / 40.2 |
| 250 | 73, 38.0 / 40.1 | 80, 39.5 / 40.2 |
| 325 | 82, 38.1 / 40.4 | 89, 39.7 / 40.3 |
| 400 | 87, 38.6 / 40.4 | 96, 39.8 / 40.4 |
| 475 | 104, 39.5 / 40.4 | 97, 39.8 / 40.4 |
| 550 | 106, 39.5 / 40.4 | 106, 39.8 / 40.2 |
| 625 | 130, 39.5 / 40.3 | 133, 39.5 / 40.3 |
| 700 | - | 223, 39.5 / 40.3 |

\* 200 km/h for 0.1.3 was run with `HighAoARollPitchWeight` 3 and `ElevonSymCost` 0.2, without the floor.

**Roll for 2.5 s, then release** (AoA during the roll / after the release, roll angle by the release):

| IAS (km/h) | 0.1.4 without the floor | 0.1.4 |
|---|---|---|
| 200 | 39.5 / 40.4, 38.5 / 40.1, 27 | 39.5 / 40.2, 39.7 / 40.2, 27 |
| 225 | 38.1 / 40.5, 38.2 / 40.0, 26 | 39.5 / 40.3, 39.7 / 40.2, 28 |
| 325 | 39.8 / 40.2, 37.9 / 40.2, 37 | 39.7 / 40.3, 39.5 / 40.2, 37 |
| 400 | 39.8 / 40.4, 39.5 / 40.2, 41 | 39.8 / 40.4, 39.6 / 40.1, 41 |
| 475 | 39.9 / 40.4, 39.2 / 40.0, 46 | 39.8 / 40.4, 39.6 / 40.1, 41 |
| 550 | 39.8 / 40.4, 39.4 / 40.2, 54 | 39.8 / 40.2, 39.6 / 40.4, 52 |
| 625 | 39.5 / 40.2, 39.4 / 40.1, 68 | 39.5 / 40.3, 39.5 / 40.2, 67 |

**Worst distance from 40 over all of these: 0.54 deg** (0.1.3 with pitch-only canards: 2.0).

**Tried, with these numbers:**
- **`HighAoARollPitchWeight`:** 5 and 10 hold the same above 250 km/h but slip at 200 (38.5).
- **`CanardLiftFloor`:**
  - 28 and 22 hold the AoA but cost up to 2/3 of the roll at 200-250 km/h (8 / 14 deg against 27).
  - 12 and 14 give about the same results as 16.
- **`ElevonSymCost` 0.05:** the elevons traded pitch for roll (37.9 at 325 km/h). 0.2-0.5 hold.
- **No effect or worse:** a move price on the canards, a lower `AoAGain`, and `AoACaptureBrakeBlend` 0 or 1.

**Unchanged:**
- **Hands-off at 500 km/h:** AoA 2.2-2.9.
- **900 km/h pull:** 9.33 g.
- **Full push 200-350 km/h:** mean AoA -29.1 (limit -30).
- **1 g full roll at 600 km/h:** 171 deg/s peak (0.1.3: 161).

**Engine-out full pull at 300 km/h:** AoA 40.0-40.3, sideslip 2.3 deg max (0.1.3: 1.2), bank excursion 3 deg.
- Pitch-only canards on their own, without the floor, gave 7 deg of bank and 2.3 deg of sideslip in the same case.
- The floor brought the bank back to 3.

**Steady full aft, no roll** (engine-out case, 300 km/h):
- Elevons: mean 0.4 deg.
- Fins: toed 27 deg, the nose-up way.
- Canards: about 17 LE down.
- The lower elevon trim cost doesn't move the elevons much here: the fins' toe and the canards already cover the trim, and the allocator picks the cheapest.

## Limits
- **Which AoA:** the bench's own body AoA reads 0.3-0.5 deg above the FCS's (a different reference point). The figures above are the FCS's, as in your logs.
- **Bench vs game:** the bench and the game disagreed on fin positions in the steady hold (0.1.3 notes), so treat the band as a bench result until a log confirms it.
- **Not covered:** roll entries before the AoA has reached 40, and full stick reversals.
