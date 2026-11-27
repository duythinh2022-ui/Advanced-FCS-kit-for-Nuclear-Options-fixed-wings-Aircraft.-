# J-20B Weilong FCS 0.1.1: AoA limit 40, canards on roll, canard leading-edge-down travel 45

Base: 0.1.0 (rebuilt 01:58, the bridge fixes). **Status:** compiled against the game's assemblies, benched. **Not flown.**

## Tom (24 Nov 02:04)
"roll at max aoa often undershoots the aoa limit to 38, and the roll start is sometimes slow. The jet can sustain 40 most of the times, not 42. Increase positve deflection for canards to 45, make them do roll, and the trailingflaps still go a lot of deflection while turning at high aoa."

## Changed
| Setting | 0.1.0 | 0.1.1 |
|---|---|---|
| `AoALimitPositive` | 42 | **40** |
| `CanardSymmetricOnly` | on (pitch only) | **off**: canards are pitch, roll and yaw effectors |
| `CanardRangeLEDown` (positive in the mod's sense) | 25 | **45** (clamp now 5-60) |
| `CanardRangeLEUp` | 25 | 25 (clamp now 5-45) |

- **Your existing dev config:** a dev file written by 0.1.0 has no `J20ConfigRevision` line. Each of these three values is moved once, and only if it still holds the 0.1.0 default. The log line is `J-20B FCS 0.1.1 defaults: ...`.
- **The canards fly unpaired.** The mod gives its canards no roll or yaw gain, so the position pairing leaves each canard as its own effector.
  - I tried pairing them the Revoker's way (common part at `StabTrimCost`) and it was worse at the limit. Full aft + full roll at 450 km/h dipped the AoA to 31.7 deg (38.5 unpaired) and rolled 66 deg in 3.9 s (74 unpaired). At 600 km/h it rolled 79 deg with 5.3 deg of sideslip (99 deg and 2.6 unpaired). So unpaired stays.

## Bench (0.1.0 at a 40 deg limit vs 0.1.1)
Full aft from the trim speed, full roll at 3.1 s, 3000 m.

| IAS (km/h) | roll at 0.5 s (deg/s) | roll in 3.9 s (deg) | AoA during roll | max sideslip (deg) |
|---|---|---|---|---|
| 300 | 5.6 -> 9.9 | 56 -> 73 | 39.3-41.0 -> 38.0-40.2 | 3.3 -> 2.6 |
| 450 | 7.2 -> 9.5 | 61 -> 74 | 38.3-40.8 -> 38.5-40.3 | 3.4 -> 3.5 |
| 600 | 9.3 -> 25.7 | 69 -> 99 | 37.3-40.4 -> 37.4-40.9 | 3.1 -> 2.6 |

**The roll onset:**
- 0.1.0 rose to 8 deg/s, then sagged to 5 for about 0.4 s while the sideslip built (this is the slow start you saw).
- 0.1.1 rises without a dip: 0 -> 12 -> 18 -> 23 deg/s at 450 km/h.
- The canard split swings sign once mid-roll (+19 -> -18 deg), as the allocator moves the canards from roll to yaw.

**Held full aft (no roll):**
- 0.1.1 holds 38.5-40.0 deg at 300-600 km/h with sideslip under 1.1 deg.
- Average elevon deflection is 29 / 29 / 22 deg TE up at 300 / 450 / 600 km/h (0.1.0: 30 / 31 / 21).

**1 g full roll:** 300 / 500 / 800 km/h end at 126 / 168 / 217 deg/s.
- With symmetric canards, same case: 132 / 189 / about 237.
- The canard split takes over part of the roll from the outboard elevons (outboard split 42 -> 23 deg at 500 km/h), and the roll-authority estimate comes out 5-10 % lower.
- That is the cost of canard roll at low AoA. `CanardSymmetricOnly = true` brings 0.1.0 back.

## The trailing-edge deflection in high-AoA turns: measured, not changed
The elevons take over pitch as the AoA passes the canard's lift peak.

| Limit | Hold (deg) | Elevon avg TE up (deg) | Canard (deg) | Fin toe (deg) | Effector pitch moment asked / got (deg/s^2) |
|---|---|---|---|---|---|
| 36 | 36.4 | 9 | -0.5 | 5 | 49 / 44 |
| 38 | 37.8 | 20 | +2.0 | 12 | 50 / 41 |
| 40 | 39.5 | 29 | +2.8 | 21 | 52 / 39 |

Full aft held at 450 km/h (pitch rate ~18.5 deg/s, 3000 m).

**What these numbers show:**
- The effectors are asked for about the same ~50 deg/s^2 of nose-up at every limit.
- The canard's share falls from 44 to 39 as its local AoA passes its peak (CL 2.9 at 36.6 deg local, 2.7 at 39, 2.4 at 42). The elevons and the fins' toe fill the gap.
- At 300 km/h the 36 deg hold needed 2.5 deg of elevon; at 38-40, 29.
- Part of it is the settled stall window: it keeps the canards 2-3 deg leading edge down at 40 deg AoA. Static map: canard 0 instead of +5 is worth about 8 deg/s^2, roughly 8 deg of elevon. Without the window the canards parked past their stall at 600 km/h (-18 deg) and the AoA fell to 21-32.

**Why the 45 deg canard travel doesn't help here:** the extra leading-edge-down travel is the nose-down side and adds nothing at the limit. It adds nose-down reach at low and negative AoA.

**What would lower the elevon use:**
- A lower AoA limit: 38 -> about 20 deg average, 36 -> 2-10.
- More nose-up from the airframe: forebody past x1.5, or the CG further aft than 0.6 m ahead of the mains.

## Install
- `BepInEx\plugins\J20B_Weilong.FCS\J20B_Weilong_FCS.dll` 0.1.1 (native core inside).
- Or the C#-only build plus the optional `J20B_FcsCore.dll` beside it.
