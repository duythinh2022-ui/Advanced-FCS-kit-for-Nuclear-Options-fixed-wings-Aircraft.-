# F-22E FCS 2.4.7

Base: 2.4.6 as installed (md5 24f2b2f…).

Changes:
1. The lift buff on the wing and fuselage now ramps in from 12° AoA instead of 17°. Same peaks, still pitch-neutral, lift only.
2. Fix for the nose dip after releasing the stick out of a corkscrew (and other fast AoA unwinds with the stick centred).

Not flown. Bench only.

## 1. Lift ramp from 12° (dev "5. Airframe", all variants, AI included)
- **New defaults:** `WingLiftRampStart` 12 and `BodyLiftBoostStart` 12 (2.4.6: 17). Peaks unchanged: wing ×1.20 of its old 30° lift at 36.6°, fuselage ×1.20 at 42°.
- **Existing files:** a dev file still on 17 is moved to 12 once (`AirframeRevision` 17). A value you set yourself stays.
- **Pitch-neutral split** re-solved for the 12° start: the four fore/aft tables now run 12–60°.
- **Negative AoA, drag and control surfaces:** unchanged (bench: 0.000 drag difference).

Lift area against 2.4.5 (bench, ×):

| AoA | 12° | 15° | 20° | 25° | 30° | peak |
|---|---|---|---|---|---|---|
| Main wing | 1.001 | 1.010 | 1.043 | 1.083 | 1.119 | 1.200 @ 36.6° |
| Fuselage | 1.001 | 1.007 | 1.036 | 1.080 | 1.130 | 1.200 @ 42° |

The curves stay monotone up to the peaks. Pitching moment with stabs, nozzles and flaperons neutral is within 0.12 deg/s² of 2.4.5 from 12 to 60°.

## 2. Post-corkscrew nose dip
**What the 24 Oct 22:26 log showed:**

| Release | q min (deg/s) | time below −2 |
|---|---|---|
| Corkscrew, 118.3 s | −3.7 | 0.34 s |
| Corkscrew, 69.6 s | −1.4 | 0 |
| Straight pull, 16.4 s | −0.2 | 0 |

### Cause (found on the bench, which now reproduces it: q min −2.2 to −2.6)
The PDF's Part B was right: the allocator settled short.
- **Where the nozzles stuck:** for 0.15 s at a time they stayed frozen between two of their grid points (−3.8°, then 0.1°) while the modelled pitch miss grew to 2–10 deg/s² nose-down.
- **Why the main solve didn't move them:**
  - The roll and yaw targets were missing by ~30 deg/s².
  - The stabs sat on their stall plateau.
  - So every joint Gauss-Newton step was rejected by the line search.
- **Why the last pass didn't move them either:** it only tries each effector's grid points (the nozzles': every 4°). Neither neighbour beat where they were, so they stayed.
- **Second, smaller part:** after the solver fix, a lag remained.
  - As the AoA unwinds 57 → 48°, the fuselage loses ~15 deg/s² of nose-up in half a second.
  - INDI sees that through its filter, and the nozzles take another ~0.1 s on top.
  - The 1.9.0 hybrid lead can't cover this: it is faded out above 25–40°, and its stalled-foil slopes have the wrong sign up there.

### Fix
| Setting (dev "4. Gains") | What it does | Scope |
|---|---|---|
| **`AllocNozzleNewton`** (true) | The allocator's last pass also tries a local Newton step for the nozzles, not only their grid points. | All flight. It only ever takes a lower-cost solution. |
| **`HybridIndiHighAoALead`** (0.15 s) | Disturbance-rate lead: the airframe's own pitch acceleration (measured less the effectors' modelled share) is taken forward by its rate × 0.15 s. | Gated: above 25–40° AoA and pitch stick released only. |

More on the lead:
- Pitch damping is left out of it, because it is the rate loop's own doing; leading it fought the capture.
- It is model-free, so it follows whatever is moving the nose: the airframe, a gravity-driven AoA change, coupling.

Either setting can be turned off to get 2.4.6 back.

### Bench: corkscrew releases (full aft + full roll to ~65°, then both released)
| Case | 2.4.6 q min | 2.4.7 q min | 2.4.6 below −1 / −2 | 2.4.7 below −1 / −2 |
|---|---|---|---|---|
| 380 km/h start, 3 km | −2.53 | **−0.43** | 0.60 / 0.30 s | 0 / 0 |
| 330, 3 km | −1.31 | **−0.59** | 0.18 / 0 | 0 / 0 |
| 280, 3 km | −2.35 | **−0.75** | 0.40 / 0.18 | 0 / 0 |
| 380, 1 km | −2.61 | **−0.68** | 0.62 / 0.32 | 0 / 0 |
| 250, 3 km | −2.25 | **−0.38** | 0.68 / 0.22 | 0 / 0 |
| 450, 3 km | −2.53 | **−1.76** | 0.60 / 0.32 | 0.28 / 0 |
| 330, 6 km | −1.11 | **−0.50** | 0.12 / 0 | 0 / 0 |
| 300, 1 km | −1.10 | **−0.25** | 0.20 / 0 | 0 / 0 |
| 220, 3 km | −2.21 | **−0.44** | 0.46 / 0.22 | 0 / 0 |

- **Median q over the dip:** −0.55…+0.35 → −0.12…+0.42.
- **Against the PDF's acceptance:** median ≥ −0.3 and no time below −2 are met in every case; worst ≥ −1 in 8 of 9. The 450 km/h case still touches −1.76 briefly.
- **Straight pull release** (suite S2, 24 cases): worst −0.3 → −0.4.

### Falling leaf (full aft, alternating full pedal for 8 s, then release)
| Case | AoA during the leaf | Release q min |
|---|---|---|
| 200 km/h, AB | 64.1–65.8°, same as 2.4.6 | 0.12 → −0.49 |
| 200 km/h, idle | 43.0–46.4° (2.4.6: 42.5–46.3) | 0.02 → −0.23 |
| 300 km/h, AB | 64.6–65.7°, same | 0.12 → −0.49 |

The bench leaf already held its AoA within ±1° on 2.4.6. With the stick held, only the allocator fix applies there; the lead is for the released stick.

### Regression suite S1–S7 (2.4.6 → 2.4.7)
- **Identical within rounding:** S1 (worst −4.2, mean −2.1, t90 1.07), S3 rolls, S5 (rMax 25.8 / 34.5 → 25.9 / 34.7) and S7 (q −0.8…0.5, −1.0…0.6).
- **S4, held pull + roll:** bank at 1 s at 65° is 49 / 74 / 94 / 110 (was 49 / 74 / 93 / 108), so no roll cost.
- **S6, high-AoA release + roll:** q min −0.5 → +0.1 and −0.9 → −0.2.
- **Roll reversals with the stick released** (the 24 Oct 00:10 input at 526 and 400 km/h, idle and half throttle): sideslip, AoA, nz and roll rate unchanged.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` (md5 843b9766d44e84af4b35c51964e7e737) and `meta.json` 2.4.7.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` (e1c85211692d9c1bccba129702ddc19f).
- **Backup:** 2.4.6 is `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.6.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.7-release`.

## Not verified
- Not flown. The bench's stall-region aero is the game's charts rebuilt, and earlier bench dips were smaller than the game's; check the next log's corkscrew releases against the table above.
- The 450 km/h case still has a short dip to about −1.8.
