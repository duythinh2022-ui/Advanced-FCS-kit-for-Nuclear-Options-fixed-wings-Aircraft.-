# FS-20 Vortex FCS 0.3.1 — elevons as flaps, wing vortex lift, no flap bias, stagger 40 / 55, faster falling leaf

Build of 17 Nov 2026. **Benched, not flown.** On top of 0.3.0 (the shared asymmetric-load code), with the 0.2.12 airframe test
build's changes merged in, and the shared file brought back in line with the other six jets.

## Base and shared code
- Base: the 0.3.0 source restored to the 0.3.0 release folder; checked here IL for IL against the installed 0.3.0 DLL
  (1331 / 1331 method bodies identical).
- `AsymShared.cs` taken from Ifrit 1.5.0 (namespace line only changed) and the two `FcsController` engine-out lines moved to
  `EngSplitOut()`: `checkshared.py` gives AsymShared `07b1e78b2722f37a`, AsymCap `f1b8d2915c6e087c`, CoupledCap `6f66ef4087ade420`,
  the same as the other jets. The new engine-out code only acts with `EngineOutZeroThrust` on (not on the Vortex).

## What changed

### 1. No flap bias (`ElevonManeuverDroop` false)
- In CTOL the elevons no longer droop.
  - 0.2.11 put up to 23 deg of TE-down on them, with AoA and load.
  - In the 11:50 log this parked the inner pair at 27-28 deg local AoA, on their 26 deg stall peak, during the asymmetric-load left roll.
- The STOVL droop is a separate schedule and is unchanged.

### 2. Elevons act as flaps on the wing (`ElevonFlapModel`)
- **How the strip was sized.** Strip theory from the part geometry (collider spans, panel areas):
  - The inner elevon has ~4.0 m^2 of wing ahead of it: wing2b, wing2a and the ends of wing1a / wing1b.
  - The outer elevon has ~3.9 m^2: wing2b, wing2a and the wingtip.
  - Chord fraction is ~0.21 for both, so thin-airfoil flap effectiveness tau = 0.56.
- **The camber term.**
  - It is spread over that whole strip (`InnerStripArea` 1.48 / `OuterStripArea` 1.44 x wing2b).
  - It follows the **wing's** lift curve, not the elevon plate's staggered one. The flap stops working when the wing stalls, not at AoA + deflection.
  - It has plain-flap deflection effectiveness: full to 10 deg, 0.8 per deg at 20, 0.6 at 30 (30 deg acts like 26). These are DATCOM-style values, approximate.
- **K and the plate area.**
  - `ElevonFlapK` 0.21 = 0.56 x (strip + plate geometry) - the plate's game area.
  - The game gives each elevon plate 2.0 m^2 against ~1.05 m^2 of geometry, so the plates already carry most of the flap effect at low AoA.
  - The plates keep that area (`ElevonPlateLiftScale` 1). On the bench, 0.53 (geometric) left the jet 16 deg/s^2 nose-up unstable at 15 deg AoA, because that area is also the trailing edge's share of the wing's lift.
  - Net: about a third more camber lift per degree than 0.2.11 at small deflections, a little less at full deflection.
- **Past the wing's lift peak.**
  - The camber term keeps at least 35 % of its attached lift per degree (`PostStallFlapEff`) instead of reversing with the wing's falling curve. This is an assumption, tunable.
  - From 45 deg AoA (`FlapCamberHiAoA`) it blends over 15 deg back to the 0.2.11 term (fully separated flow).
  - Without that blend, the 70 deg falling leaf lost a fifth of its yaw rate and its stop took twice as long.

### 3. Delta-wing vortex lift on the wing panels (`WingVortexLift`, `WingLiftSchedule`)
- The wing panels (wing1a/1b, wing2a/2b, wingtip) went flat at 17 deg on the stock foil (CL 0.90-0.94 to 30 deg).
- They now keep lifting: CL 1.20 at 25, 1.33 at 30, 1.37 at the 33 deg peak, 1.20 at 40, stock again by 50-55.
- The extra lift is a normal force, so its drag is dCL x tan AoA.
- The wing sits aft of the CG, so the forebody schedule (`ForeLiftSchedule`) was raised 16-52 deg to keep 0.2.11's pitch balance. Bench `cmsweep`, every surface neutral: within 0.3 deg/s^2 from 0 to 55 deg AoA.
- The schedule moves once if your file still holds the 0.1.0 default.
- Normal force x area against 0.2.11: +7 % at 20 deg, +24 % at 30-35 deg.

### 4. Stall stagger 40 / 55 (was 26 / 45)
- `InnerElevonReversalAoA` 40 and `OuterElevonReversalAoA` 55. They move once if your pair is exactly 26 / 45.
- **How it was picked.**
  - Sweep: inner 20-45 x outer 30-60 on the 0.3.1 airframe.
  - Measures: trimmed lateral authority 0-70 deg AoA at 300 and 500 km/h (new bench mode `vauth`), asymmetric-load rolls, falling leaf and leaf reversal.
  - Several pairs open a hole: 0-3 deg/s^2 somewhere at 55-67 deg at 300 km/h. 26 / 45 has one at 60 and 65 deg.
  - 40 / 55 has none, the most asymmetric-load left roll (tied with 45 / 50) and the quickest leaf reversal.

### 5. Falling leaf rate (`LeafRateCapFadeLo` 45 / `LeafRateCapFadeHi` 60)
- The roll-pace rate cap's share fades out from 45 to 60 deg AoA. Below 45 nothing changes.
- The cap's reversal-sideslip model held the 70 deg leaf at ~22 deg/s against a 36-38 deg/s airframe limit. The pacing window still paces stops and reversals there.
- Set Hi <= Lo for 0.3.0.

### Bench fix (bench only)
- The bench applied only one elevon's camber per wing (both share wing2b), where the game applies both.
- Every comparison below is on the fixed bench.
- With the fix, the asymmetric roll-stop / reversal departures the bench showed earlier today no longer happen on either build (AoA <= 15).

## Bench (0.3.0 vs 0.3.1, same bench; 0.3.0 = 0.3.1 with the new keys off and stagger 26 / 45)

**Trimmed lateral authority** (elevon differential, worse side, deg/s^2 velocity-vector roll; full = with rudders and yaw nozzle)

| AoA | 300 km/h 0.3.0 | 300 km/h 0.3.1 | 500 km/h 0.3.0 | 500 km/h 0.3.1 |
|---|---|---|---|---|
| 10 | 253 (full 298) | 270 (315) | 703 (830) | 749 (920) |
| 20 | 161 (230) | 206 (256) | 448 (589) | 572 (709) |
| 30 | 106 (166) | 145 (205) | 295 (394) | 403 (497) |
| 40 | 63 (117) | 78 (133) | 174 (253) | 216 (287) |
| 50 | 33 (94) | 49 (107) | 112 (186) | 149 (209) |
| 60 | 5 (68) | 25 (92) | 58 (126) | 83 (152) |
| 70 | 3 (69) | 8 (74) | 19 (89) | 33 (102) |

**Asymmetric load, heavy** (1760 kg on wing2a_R, CG ~0.4 m right, full aft stick, full left roll): bank in 5 s

| case | 0.3.0 | 0.3.1 |
|---|---|---|
| 470 km/h, throttle 0.6 (the 11:50 log's 131 s) | 98 deg, max sideslip 19.1 | 125, 19.0 |
| 330 km/h, AB | -29 (rolled the wrong way), 15.3 | 137, 16.8 |
| 300 / 400 km/h, AB | -63 / 135 | 163 / 149 |
| 400 / 550 km/h, half throttle | 71 / 142 | 119 / 185 |
| 330 km/h, half stick | 37 | 130 |
| 250 km/h, 1000 kg | 213 | 203 |
| right roll (toward the heavy wing), 330 km/h | 339 | 314 |

**0.3.0's own asymmetric-load set** (650 kg on wingtip_L, 1000 m, throttle 0.8, full aft then full roll each way)

| | 0.3.0 | 0.3.1 |
|---|---|---|
| 250 km/h: roll +/-, peak AoA (limit) | +41 / -41 deg/s, 16.5 (12.0-17.1) | +35 / -32, 19.0 (12.6-20.1) |
| 400 km/h | +57 / -68, 12.9 | +47 / -65, 14.5 |
| 600 km/h | +75 / -81, 13.1 | +69 / -87, 13.6 |
| store drops in a pull (1.5 / 4.0 / 5.7 s, throttle 0.75) | held AoA 32.1 / 12.0 / 12.0 / 12.0 | 31.6 / 12.0 / 12.0 / 12.0 |

With 650 kg the limiter's roll-reserve limit rises further during the roll on the 0.3.1 airframe (it has more roll in hand at
each AoA), so the jet rolls at 1.5-2 deg more AoA and 10-20 % slower there. That is the shared limiter reading the new airframe.

**Falling leaf** (70 deg AoA, full roll, release)

| | 0.3.0 | 0.3.1 |
|---|---|---|
| steady yaw rate | 22.5 deg/s | 35.7 deg/s |
| stop after release | 2.03 s | 1.95 s |
| full-stick reversal: time to cross zero yaw | 1.77 s | 1.95 s |
| max sideslip | 2.7 deg | 4.6 deg |

**Regression suite** (`bench.sh suite`)
- Stop / reversal sweep: worst sideslip 6.0 / 7.3 -> 4.6 / 5.8 deg, mean roll 91 -> 97 deg/s.
- Regression set: within noise, except:
  - Pull-then-roll at 250 km/h: max roll 25 -> 19 deg/s.
  - Corkscrew -55 at 170 km/h: sideslip 1.0 -> 2.2 deg.
  - Pull-through with roll released at zero: sideslip 6.3 -> 2.1 deg.

**STOVL** (hover, VL, VTO, deceleration, transition): same to within a few metres.

## Not fixed / worse
- **The heavy-load left roll is held by the shared limiter's 12 deg floor** (`AsymMinAoACap`, the same 12 in all seven mods).
  - Since 0.3.0 the coupled check sets the pitch cap at 25-28 deg, so with 1760 kg on one side the roll-reserve limit is what holds. It wants less than 12 and sits on the floor.
  - 0.2.11's pitch cap held 10.4-10.9 deg, below the floor.
  - Bench, 0.3.1, heavy load, bank in 5 s / max sideslip:

    | floor | 470 km/h, throttle 0.6 | 400 km/h, half throttle | 300 km/h, AB |
    |---|---|---|---|
    | 12 (shipped) | 124 / 19.0 | 118 / 15.2 | 163 / 17.1 |
    | 10 | 316 / 4.0 | 264 / 5.9 | 224 / 9.0 |
    | 8 | 483 / 2.6 | 415 / 2.4 | 429 / 4.3 |

  - The floor is shared logic, so it is left at 12 here. A lower floor has to be checked against 1 g at low speed with the load: below the 1 g AoA the jet could not hold level flight on the cap.
- **Roll at high AoA, then an immediate pushover, at 560 km/h: max sideslip 4.1 -> 11.9 deg.** The extra falling-leaf rate is still in the yaw when the AoA unloads. With `LeafRateCapFadeHi` 0 it is 3.3. The same case at 600 km/h is fine.
- **Allocator command flipping while yaw is saturated** (asymmetric load, full aft, 300-400 km/h): 130-220 jumps of more than 15 deg on `elevator_L` in 5 s, as on 0.3.0. The servos filter most of it (surface within ~±2.5 deg). It is in shared code.
- **The bench is optimistic laterally at the 11:50 log's 131 s condition:** the game rolled at 10 deg/s with 14.7 deg of sideslip, the bench ~47 deg/s and 8 deg on 0.2.11. Expect smaller gains in the air.

## Install
- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.3.1; 0.3.0 kept as `Vortex_FS20_FCS.dll.0.3.0.bak`. `meta.json` updated.
- The first spawn moves `ForeLiftSchedule` and the stagger pair (26 / 45 -> 40 / 55) once if they still hold the old defaults.
- Every new key's false / off value restores the 0.3.0 airframe.

## Keys (all new ones in the Dev config "5. Airframe" unless noted)
- `ElevonManeuverDroop` false
- `ElevonFlapModel` true, `ElevonFlapK` 0.21, `InnerStripArea` 1.48, `OuterStripArea` 1.44, `ElevonPlateLiftScale` 1, `PostStallFlapEff` 0.35, `FlapCamberHiAoA` 45
- `WingVortexLift` true, `WingLiftSchedule`
- `LeafRateCapFadeLo` 45, `LeafRateCapFadeHi` 60 (main config, "6. Lateral governor"; take effect at once)
- Airframe keys take effect on the next spawn. Every new key's false / off value restores 0.2.11.
