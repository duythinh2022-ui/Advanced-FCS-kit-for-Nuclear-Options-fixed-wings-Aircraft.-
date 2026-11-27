# J-20B Weilong FCS 0.1.5: trailing-edge camber coupling done properly

Base: 0.1.4. **Status:** compiled against the game's assemblies, benched. **Not flown.**

## Tom (24 Nov)
- 10:28: "does camber coupling really work, like the irl j-20 has really good roll rate. The trailing flaps should move the entire airflow over the wings so there must be a lot of authority for the flaps right?"
- 10:34: "do the coupling properly but canards still pitch only. And in the irl footage, the j-20 has very high roll rate at least up to 20aoa"

## What was wrong (0.1.0-0.1.4)
**1. The inboard elevons cambered the LERX.**
- Their camber share went on `wingRoot`, the part the mod hangs them from.
- `wingRoot`'s force point is the strake / LERX: 1.67 m out, 4.78 m ahead of the CG. The elevon itself is 3.19 m out, 4.17 m behind the CG.
- Per degree, the camber lift there was nose-up and nearly cancelled the elevon's own nose-down (area x arm: +10.9 against -10.1).
- It also rolled at half the elevon's arm.
- The panel actually ahead of the elevon is `wing1` (12 m², 3.36 m out, 0.88 m ahead of the CG).

**2. At AoA the camber share vanished.**
- The share is a turn of the host section's lift direction, evaluated on the section's own curve (`Camber.Delta`).
- The J-20B wing foil rises 0.064/deg to about 15 deg, then only 0.004-0.006/deg from 17 to 50.
- So above about 17 deg the share was about 7 % of its low-AoA size.

## Changed
| Setting | 0.1.4 | 0.1.5 |
|---|---|---|
| `InnerCamberOnWing1` (new, 5. Airframe) | (on `wingRoot`) | **on**: the inboard elevons' camber goes on `wing1`, as a force |
| `FlapCamberCoupling` | 0.61 | **0.44** |
| `AileronCamberCoupling` | 0.47 | **0.44** |
| `CamberHoldAoA` / `CamberFadeAoA` / `CamberRetain` (new) | - | **22 / 40 / 0.35** |

**Coupling sizes.** Both are thin-airfoil flap effectiveness, sized from the bench's force points. The host chord comes from the spacing between the host's and the elevon's centres of lift: (spacing - 0.25 x elevon chord) / 0.75.

| | Host chord | Flap chord ratio | Effectiveness | Host share |
|---|---|---|---|---|
| Inboard (`wing1`) | 6.25 m ahead of the 1.43 m elevon | 0.19 | 0.53 | (0.53 x 14.43 - 2.43) / 12 = 0.44 |
| Outboard (`wingtip`) | 4.05 m ahead of the 1.14 m elevon | 0.22 | 0.575 | (0.575 x 5.27 - 1.27) / 4.0 = 0.44 |

**How the camber is now modelled.**
- The camber increment is evaluated on its own table: the section's low-AoA lift slope (±4 deg), full to 22 deg section AoA, falling to 0.35 of that by 40 deg, held to 60 deg, then to zero by 90.
- Past 90 deg (reversed flow) it uses the section's own curve. Drag is the section's own.
- A flap shifts the section's lift curve up instead of turning it, and on a LERX / canard-vortex wing much of that shift survives past the plain curve's knee.
- The FCS's model, the in-game force (CamberForce) and the bench all use the same table.
- `CamberHoldAoA` 0 restores the old behaviour (the section's own curve).

**Unchanged:**
- The inboard pair's lift direction stays at rest (the share is a force).
- The canards stay pitch only.
- `wingRoot`'s camber direction is now held at rest, like the other unhosted ones.

**Your existing dev config:** `J20ConfigRevision` goes 3 -> 4.
- `FlapCamberCoupling` and `AileronCamberCoupling` move to 0.44 once, each only if still at its 0.1.4 default.
- The log line is `J-20B FCS 0.1.5 defaults: ...`.

**Shared code:**
- `HingeFlapEffector` gains `forceCamber` (default off).
- `CamberForce` applies a hinge's camber force when either `ElevonStallStagger` or `forceCamber` is set.
- `StallStagger` gains `CamberTable`.
- Other jets are unchanged.

## Bench (0.1.4 coupling vs 0.1.5)
FCS on, 3000 m, throttle 1.

**Roll at a set AoA** (roll stick from 2.5 s; pitch stick fixed):

| Case | 0.1.4 | 0.1.5 |
|---|---|---|
| 20.5 deg AoA, 500 km/h: peak roll rate / roll in 1.5 s | 64 deg/s / 72 deg | 83 deg/s / 102 deg |
| 20.5 deg AoA: AoA during the roll | 20.5-27.5 | 20.1-22.0 |
| 20.5 deg AoA: elevons during the steady roll | ±20 deg | ±1 deg (inboard ±6) |
| 29 deg AoA, 350 km/h: roll in 1.5 s | 33 deg | 47 deg |
| 1 g, 400 / 600 / 800 km/h: peak roll rate | 192 / 207 / 242 deg/s | 184 / 207 / 244 deg/s |

**At 1 g nothing changed.** There the roll rate is at the FCS's roll-rate ceiling (`MaxRollRate` / the mod's own schedule, about 210 deg/s at 500 km/h), not at the surfaces' limit.

**Full aft + full roll at the 40 deg limit:**
- Roll angle in 4.9 s, mean of 200-700 km/h: 112 -> 135 deg.
- Roll by the release (2.5 s), 200-625 km/h: 42 -> 56 deg mean.
- AoA band, the FCS's own AoA: 39.3-40.5 (0.1.4: 39.5-40.4). Worst distance from 40: 0.71 deg, at 200 km/h; everywhere else 0.5 or under.

**Unchanged:**
- **Hands-off at 500 km/h:** AoA 2.2-2.9.
- **900 km/h pull:** 9.33 g.
- **Full push 200-350 km/h:** mean AoA -29.1.

**Engine-out full pull at 300 km/h:** AoA 40.0-40.4, sideslip 1.1 deg (0.1.4: 2.3), bank 1 deg (0.1.4: 3).

## What limits the roll at 20 deg now
**It isn't the elevons:** they sit at ±1-6 deg through the steady roll.
- At 20 deg AoA a roll about the flight path needs the yaw to build with it.
- The canted fins are the only yaw effector, and their yaw comes with a bigger opposite roll: at full deflection, 400 km/h, -93 roll / +77 yaw deg/s² in stability axes.
- The lateral law's pacing (`RollPaceRateBlend` 0.65, `RollPaceRateSlip` 1, `RollStopSideslip` 6) holds the roll at what the yaw can follow within the sideslip margin.

**Tried on the bench:**

| Setting | Roll in 1.5 s at 20 deg AoA | Sideslip | AoA during the roll |
|---|---|---|---|
| default | 102 deg | 6.6 deg max | holds |
| `RollPaceRateBlend` 0.4 | 141 deg | 11.6 deg | falls to 13 deg |
| `RollPaceRateBlend` 0 | 191 deg | 15.5 deg | falls to 4.7 deg |

Lower values roll faster only by flying more sideslip and dropping AoA, so I left them.

## Limits
- **The new coupling sizes are my estimate.** They come from the force-point geometry (the extract has no true chord or span). The camber-shape numbers (22 / 40 / 0.35) are a modelling choice, not data.
- **Not covered:** roll entries before the AoA has reached 40, full stick reversals, and damage cases.
