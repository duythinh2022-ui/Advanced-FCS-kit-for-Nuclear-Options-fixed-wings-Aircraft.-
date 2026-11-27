# F-22E Strike Raptor FCS 4.1.5: the main brakes help the ground steering (port of Su-30 0.5.3 + 0.5.4)

Base: 4.1.4. Tom, 11 Nov 21:59: "port the brakes to raptor and ifrit". The ground code of all three jets was the same before the port (F-22E 2.4.14 INDI nose wheel; the ground methods differ only in comments), so the Su-30 code went in unchanged.

**Changelog:**
- **Turn stop.** After you release the pedal, the brake on the outside main now helps the nose wheel stop a ground turn's yaw. It takes only what the nose tyre can't grip: full brake authority to 15 kt, faded out by 25 kt. On the bench, release stops at 7–17 kt are 15–20 % shorter (10 kt, full pedal: 1.38 → 1.12 s) and the heading overshoot after release is smaller. The cost is 5–6 kt more speed lost per stop from a full-pedal turn at 7–17 kt.
- **Light nose.** When the nose goes light mid-stop, the nose wheel keeps its working angle instead of swinging to full opposite lock and then 0.
- **Pedal brake (drift).** While the pedal is in, the main on the pedal's side is braked by |pedal| × 0.5. This jet had no stock differential braking at all: both mains read the left pedal, so stock left pedal brakes both and right pedal brakes neither. Now the jet pivots harder and drifts. Bench, 3 s of full pedal from 15 kt: peak sideslip 2.6 → 9.3°, peak yaw rate 39 → 48 deg/s. It also slows the jet: 19.9 → 9.5 kt after 3 s at low thrust.
- **New dev settings:** GroundIndiBrake, GroundIndiBrakeMax (1.0), GroundIndiBrakeFullSpeed / ZeroSpeed (7.7 / 12.9 m/s), GroundIndiLightNoseHold, GroundPedalBrake (0.5).
- **Log columns:** gsBrkL, gsBrkR, gsBsL, gsBsR, gsLn.

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,676,800 bytes, SHA-1 b87afd63da3bbde707f70e72940880c35c67979e. The 4.1.4 source rebuilt here gives the released 4.1.4 DLL byte for byte.
- Benched on the new `gyaw` bench, ported from the Su-30: the FCS's own C# against the game's gear model as read from the IL. Gear data comes from Aryx's bundle (below). The same runs were done on the take-off bench and with flight cases.
- Run on the game's Mono under Wine:
  - gear accessors bound;
  - `LandingGear.FixedUpdate` patched with the new prefix/postfix (`GearSwap` state);
  - native self-test 200 / 200.
- **Not flown.**

## Gear (Aryx bundle, `Aryx_F22E_StrikeRaptor_1.0.4.dll`)
- **Nose:** lock 45°, 60 deg/s, aligning 5, μ 1.0, not braked.
- **Mains:** μ 1.0, braked, `differentialBrakeFactor` −1 on **both**.
- **Bench geometry:** 25.0 t, Iz 529,000 kg·m² (the 2.4.14 ground sim's m × 4.6²), 18 % of the weight on the nose, wheelbase 5.88 m, track 2.47 m.
- The narrow track means each unit of brake gives this jet less yaw than the Ifrit or Su-30 for the same speed cost.

## Bench (`gyaw`; no aero, so nothing above ~60 kt is meant literally)
### Release stop after a 2.5 s pedal turn
Pedal brake off in both columns, so both releases start from the same turn. Stop = yaw rate under 2 deg/s.

| kt, pedal | yaw at release | stop 4.1.4 → **4.1.5** | heading after release | swing-back deg/s | kt lost to the stop |
|---|---|---|---|---|---|
| 7, full | 33.6 | 1.20 → **0.97 s** | 21.5 → 17.9° | 0.40 → 0.22 | −1.1 → 3.9 |
| 10, full | 39.7 | 1.38 → **1.12** | 29.9 → 25.0 | 0.59 → 0.14 | −1.0 → 5.2 |
| 13, full | 38.5 | 1.37 → **1.10** | 29.9 → 25.5 | 0.57 → 0.00 | −0.5 → 5.5 |
| 17, full | 37.5 | 1.27 → **1.03** | 26.5 → 23.2 | 1.15 → 0.01 | −0.1 → 5.0 |
| 23, full | 34.3 | 1.10 → **1.03** | 21.1 → 20.4 | 1.10 → 0.49 | 0.1 → 1.6 |
| 10, half | 20.5 | 0.72 → **0.58** | 7.6 → 7.1 | 0.65 → 0.00 | −0.2 → 2.0 |
| 17, half | 34.2 | 1.02 → **0.85** | 17.4 → 15.4 | 0.77 → 0.15 | −0.2 → 3.5 |

- **All switches off** (GroundIndiBrake / GroundIndiLightNoseHold off, GroundPedalBrake 0): identical to 4.1.4 in all 10 release cases.
- **With the pedal brake on as shipped**, the turn before release is tighter: 10 kt full pedal reaches 48 deg/s instead of 40. That stop then takes 1.30 s from the higher rate.
- **GroundIndiBrakeMax at 10 kt:**

| Max | Stop | Speed lost |
|---|---|---|
| 0.4 | 1.25 s | 1.9 kt |
| 0.6 | 1.20 s | 3.1 kt |
| 1.0 | 1.12 s | 5.2 kt |

### Light nose: nose load gone 0.3–0.85 s into the release

| Speed | Stop | Heading | Swing-back | Speed lost |
|---|---|---|---|---|
| 10 kt | 1.85 → 1.50 s | 44.7 → 34.4° | 1.03 → 0.12 deg/s | −0.8 → 8.1 kt |
| 15 kt | 1.75 → 1.40 s | 41.8 → 32.8° | 1.05 → 0.04 deg/s | −0.2 → 9.0 kt |

### Drift: 3 s of full pedal, speed free under thrust (thrust/weight)
| Entry, T/W | Peak sideslip 4.1.4 → **4.1.5** | Peak yaw rate | Speed after 3 s |
|---|---|---|---|
| 8 kt, 0.15 | 6.7 → **10.4°** | 42.5 → 29.3 | 11.6 → 5.3 kt |
| 15 kt, 0.15 | 2.6 → **9.3°** | 39.2 → 48.0 | 19.9 → 9.5 kt |
| 15 kt, 0.5 | 1.2 → **2.7°** | 30.0 → 44.1 | 40.8 → 29.6 kt |
| 25 kt, 0.5 | 2.2 → **8.7°** | 25.0 → 39.4 | 51.3 → 39.8 kt |
| 40 kt, 0.15 | 0.8 → **5.7°** | 23.5 → 36.5 | 46.6 → 35.4 kt |

- **At 8 kt** the brake slows the jet so much that the yaw rate falls (42 → 29 deg/s). From about 15 kt up it turns faster and slides.
- **GroundPedalBrake 0.25:** 15 kt: 5.5° and 46 deg/s, 13.9 kt after 3 s; 25 kt: 4.9°; 40 kt: 2.1°. At 1.0, 15 kt: 11.3°, 4.8 kt after 3 s.
- **Your earlier F-22E rule** (27 Oct) was drift at about half the Ifrit's. That rule was about high-speed drift; the bench has no aero and can't show high speed. At 0.5 the F-22E now slides about as much as the Ifrit 1.1.3 on the bench: 15 kt 9.3 vs 10.1°, 25 kt 8.7 vs 7.6°, 40 kt 5.7 vs 5.0°. 0.25 is roughly half the Ifrit's. The port ships 0.5, as on the Su-30; set 0.25 in the dev config if it slides too much.

### Unchanged
- **Take-off bench** (200 km/h rotation; standing start at full throttle, pull at 14 s): summary lines identical to 4.1.4.
- **Two flight cases** (pull-then-roll at 560 km/h, push-then-pull-through + roll at 350 km/h): plant states identical row for row; FCS log identical except the timing columns (µs) and the five new columns.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.1.5. 4.1.4 kept as `Aryx_F22E_StrikeRaptor_FCS.dll.4.1.4.bak`.
- **Dev DLL:** unchanged. The new keys are in the dev config, under "Ground".
- **Release:** `C:\Games\NO mods\F22E-FCS-4.1.5-release`.
- **Mod manager:** update the version there, or its modpack may put the old DLL back.

## Files
- `FcsController.cs`:
  - `GroundSteer`: brake allocation in the release stop and the hold, light-nose hold, pedal brake;
  - `GsIndiWheel` (nose share);
  - new `GsLightNoseWheel`, `GsNoseAlpha`, `GsBrakeAlloc`, `GsPedalBrake`, `MainBrakeTarget`;
  - telemetry.
- `Hooks.cs`: `GearPrefix` / `GearPostfix` write and restore each main's brake (struct `GearSwap`).
- `Acc.cs`: `LG_braked`, `LG_dbf`, `LG_groundSpeed`, `LG_brakeStrength`.
- `Plugin.cs`: the six dev keys.
- Bench:
  - `bench/sim/GroundYaw.cs` (`gyaw`, with gear options lock / slew / align / muNose / muMain / dbfL / dbfR);
  - `bench/stubs/Game.cs` / `Acc.cs` (LandingGear fields);
  - `bench/stubs/Plugin.cs`;
  - `bench/sim/Program.cs`.

Previous notes: `claude/F22E_NO_FCS_4.1.4_notes.md`.
