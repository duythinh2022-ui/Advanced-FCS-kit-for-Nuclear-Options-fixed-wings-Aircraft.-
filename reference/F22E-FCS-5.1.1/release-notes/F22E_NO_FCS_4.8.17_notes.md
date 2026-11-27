# F-22E Strike Raptor FCS 4.8.17 (Nuclear Option)

Base: 4.8.16 (with the DisableTVC sync). **Bench only, not flown.**

Asked for (26 Nov 17:44): "at the end of the log, the stabs stucked at reverse roll pose even after has bleed of to low" (17:40 log, TVC off).

## What happened in the log
- **234 s:** a right roll released from a pull, about 35 deg AoA bleeding to 0 deg.
  - At 35 deg the inside (right) stab trailing-edge down is the intended high-AoA roll pose.
  - As the AoA bled off, the right stab stayed at -28 to -30 while the left went trailing-edge up to +20.
  - That split rolls the wrong way at low AoA. Roll fell from 172 to 105-125 deg/s while the FCS asked for more.
  - The pose let go only at 234.8 s, by which time the AoA had gone to -12 deg.
- **Across the log:** split against the FCS's own roll demand on low-AoA rolls in 7 events (see the bench section).

## Cause
- **Pitch nozzles off (4.8.13):** the two-zone stab clamp allowed a fixed 8 deg past the stab's outboard lift peak.
  - That extra travel was there for nose-down at 15-20 deg AoA.
  - At 234 s it bought no pitch at all. The stab's true pitch curve was flat from -15 deg down, in both the plant and the FCS table.
- **At -28 the stab sat on the far side of a dip in its roll curve** (plant, right-roll acceleration relative to -28 deg):

  | Right stab | -30 | -28 | -20 | -15 | 0 | +30 |
  |---|---|---|---|---|---|---|
  | Roll accel | +8 | 0 | -18 | +5 | +130 | +360 |

  The allocator follows local slopes. From -28 a step trailing-edge up loses roll, so it held the stab at the stop.
- **Not a model error:** the FCS table has the same shape as the plant (about 1.3x stronger).
- **Same mechanism as 4.8.1/4.8.2.** The 4.8.2 bound (outboard zone 3 deg short of its peak) keeps the stab on the side of the dip whose slope leads back out. 4.8.13 had opened past it when the nozzles are off.

## Change (law, one place: `ClampStabOutZone`)
- **New rule, pitch nozzles off:** a stab may travel past the 4.8.2 bound only while its own pitch curve still has slope that way.
  - "Pitch curve" means the true curve (`HonestPitchAt`), not the allocator's monotone envelope, which is flat past the extreme and hides it.
  - The travel ends where the local pitch slope falls below `TwoZoneStabClampPitchSlope` (0.5) of its mean slope inside the bound.
  - It never goes past the 4.8.13 bound (8 deg beyond the peak).
- **Why not the pitch extreme itself:**
  - Tried first (95 % of the pitch gain). At the extreme the stab's pitch and roll slopes are both zero, because both come from the same lift peak.
  - On the game's motion, the right stab parked there held the left stab trailing-edge up against it (left TE-up is adverse roll at that AoA) until the roll went unmet.
- **Pitch nozzles on:** no change (TVC battery bit-identical).
- **Dev keys** (`5. Airframe`), both take effect at once:
  - `TwoZoneStabClampPitchMargin`: true (false = 4.8.16).
  - `TwoZoneStabClampPitchSlope`: 0.5.

## Bench
World: best fit (CG 0.0, stab strength 0.85), TVC off unless noted.

**17:40 log, plant pinned to the game's motion** (AoA, sideslip, TAS, p/q/r; the FCS runs its own state).

4.8.16 reproduces the stuck pose: right stab -21 -> -28 -> -30, left +3 -> +22, unmet roll up to 116-204 deg/s^2.

| Variant | Split against the FCS's roll demand, <15 deg AoA, 7 rolls (rows) | Unmet roll (summed) |
|---|---|---|
| 4.8.16 | 126 | 33.9k |
| 4.8.2 bound only | 31 | 25.5k |
| **4.8.17 (slope 0.5)** | **15** | **27.0k** |
| slope 0.3 | 15 | 32.5k |

- **234 s:** the right stab goes trailing-edge up (the normal right-roll pose) at 233.6-233.9 s, against 234.8 s in the game.
- **The 15 remaining rows** are one slow (200 km/h) left roll, the same under every variant.

**Battery (no TVC), 4.8.16 -> 4.8.17, CG -5 / 0 / +5 cm**
- **Reversals, trim, onset:** bit-identical at every CG.
- **Pull-release-roll at 36 deg** (the 4.8.13 case):

  | CG | 4.8.16 | 4.8.17 | 4.8.2 bound only |
  |---|---|---|---|
  | -5 cm | 141.8 | 140.6 | 127.2 |
  | 0 | 141.6 | 140.0 | 115.8 |
  | +5 cm | 138.0 | 136.6 | 104.0 |

  Units deg/s. Sideslip is the same as 4.8.16.
- **Same at 45 deg:** sideslip 7.4 / 4.7 / 7.3 -> 7.5 / 7.9 / 7.4. The 4.7 at CG 0 is a knife-edge; 4.8.16 gives 7.3-7.4 either side of it.
- **Idle pull-release-roll, sideslip at 36 / 45:**

  | CG | 4.8.16 | 4.8.17 |
  |---|---|---|
  | -5 cm | 9.7 / 5.3 | 10.3 / 6.6 |
  | 0 | 10.6 / 4.7 | 10.2 / 4.8 |
  | +5 cm | 10.3 / 1.9 | 9.1 / 2.5 |

- **Half-stick, pull, push:** within 0.8 deg of sideslip and 0.8 deg/s.

**Acceptance replay (10:34 log, 57 no-TVC approaches x 3 CGs):** 167 of 171 curves identical to 4.8.16. The largest difference is 0.35 deg of AoA (w4, CG -5).

## Not verified
- Not flown.
- The free-flying replay of the 234 s roll does not reach the stuck pose: the bench plant rolls a little harder, so the roll demand is met. The evidence for the fix is the pinned replay, which shows the FCS's stab choices on the game's motion, not the roll rate the aircraft would then reach.

## Going back
- Set `TwoZoneStabClampPitchMargin` false in the dev cfg (= 4.8.16), or reinstall the `.4.8.16.bak` DLL.
