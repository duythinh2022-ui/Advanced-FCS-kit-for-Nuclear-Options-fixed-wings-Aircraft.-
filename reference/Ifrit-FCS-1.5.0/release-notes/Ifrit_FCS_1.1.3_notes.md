# KR-67 Ifrit FCS 1.1.3: the main brakes help the ground steering (port of Su-30 0.5.3 + 0.5.4)

Base: 1.1.2. Tom, 11 Nov 21:59: "port the brakes to raptor and ifrit". The Ifrit's ground code (0.5.4 INDI nose wheel) was identical to the Su-30's before its brake work, comments included, so the Su-30 code went in unchanged.

**Changelog:**
- **Turn stop.** After you release the pedal, the brake on the outside main now helps the nose wheel stop a ground turn's yaw. It takes only what the nose tyre can't grip: full brake authority to 15 kt, faded out by 25 kt. On the bench, release stops at 7–17 kt are 14–20 % shorter (10 kt, full pedal: 1.37 → 1.10 s) and the heading overshoot after release is smaller. The cost is 3.5–4.5 kt more speed lost per stop from a full-pedal turn.
- **Light nose.** When the nose goes light mid-stop, the nose wheel keeps its working angle instead of swinging to full opposite lock and then 0.
- **Pedal brake (drift).** While the pedal is in, the main on the pedal's side is braked by |pedal| × 0.5, where stock gives 0.3 and only at 3–30 m/s. The jet pivots harder and drifts. Bench, 3 s of full pedal from 15 kt: peak sideslip 6.3 → 10.1°, peak yaw rate 37 → 43 deg/s; speed after 3 s at low thrust 15.4 → 12.2 kt.
- **New dev settings:** GroundIndiBrake, GroundIndiBrakeMax (1.0), GroundIndiBrakeFullSpeed / ZeroSpeed (7.7 / 12.9 m/s), GroundIndiLightNoseHold, GroundPedalBrake (0.5).
- **Log columns:** gsBrkL, gsBrkR, gsBsL, gsBsR, gsLn.

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,258,496 bytes, SHA-1 77275c530d08ee2914dbf55e1dd2e6bdfc2507fb. The 1.1.2 source rebuilt here gives the released 1.1.2 DLL byte for byte.
- Benched on the new `gyaw` bench, ported from the Su-30: the FCS's own C# against the game's gear model as read from the IL. Gear data comes from the game's `Multirole1` prefab (below). The same runs were done on the take-off bench and with flight cases.
- Run on the game's Mono under Wine:
  - gear accessors bound;
  - `LandingGear.FixedUpdate` patched with the new prefix/postfix (`GearSwap` state);
  - native self-test 200 / 200.
- **Not flown.**

## Gear (`resources.assets`, `Multirole1`)
- **Nose:** lock 45°, 60 deg/s, aligning 5, μ 0.7, not braked.
- **Mains:** μ 0.8, braked, `differentialBrakeFactor` −1 / +1.
- **Bench geometry:** 25.8 t, Iz 645,000 kg·m² (the 27 Oct taxi-log fit, m × 5²), 24 % of the weight on the nose, wheelbase 7.07 m, track 3.94 m.

## Bench (`gyaw`; no aero, so nothing above ~60 kt is meant literally)
### Release stop after a 2.5 s pedal turn
Pedal brake off in both columns, so both releases start from the same turn. Stop = yaw rate under 2 deg/s.

| kt, pedal | yaw at release | stop 1.1.2 → **1.1.3** | heading after release | swing-back deg/s | kt lost to the stop |
|---|---|---|---|---|---|
| 7, full | 27.5 | 1.08 → **0.88 s** | 16.0 → 13.4° | 0.30 → 0.23 | −1.0 → 2.4 |
| 10, full | 38.6 | 1.37 → **1.10** | 27.4 → 23.5 | 0.81 → 0.00 | −1.0 → 3.6 |
| 13, full | 36.8 | 1.33 → **1.07** | 26.9 → 23.0 | 0.57 → 0.00 | −0.4 → 3.9 |
| 17, full | 34.7 | 1.20 → **0.98** | 22.9 → 20.1 | 0.76 → 0.03 | 0.0 → 3.6 |
| 23, full | 30.8 | 1.02 → **0.97** | 17.5 → 16.8 | 1.04 → 0.97 | 0.4 → 1.4 |
| 10, half | 17.3 | 0.65 → **0.55** | 6.0 → 5.6 | 0.57 → 0.00 | 0.0 → 1.5 |
| 17, half | 28.5 | 0.92 → **0.77** | 13.0 → 11.7 | 1.09 → 0.18 | 0.0 → 2.2 |

- **All switches off** (GroundIndiBrake / GroundIndiLightNoseHold off, GroundPedalBrake 0): identical to 1.1.2 in all 10 release cases.
- **With the pedal brake on as shipped**, the turn before release is a little tighter: 13 kt full pedal reaches 40.6 deg/s instead of 36.8, and that stop takes 1.13 s.
- **GroundIndiBrakeMax at 10 kt:**

| Max | Stop | Speed lost |
|---|---|---|
| 0.4 | 1.23 s | 1.1 kt |
| 0.6 | 1.18 s | 2.0 kt |
| 1.0 | 1.10 s | 3.6 kt |

### Light nose: nose load gone 0.3–0.85 s into the release

| Speed | Stop | Heading | Swing-back | Speed lost |
|---|---|---|---|---|
| 10 kt | 1.80 → 1.43 s | 39.9 → 30.4° | 0.50 → 0.22 deg/s | −0.9 → 6.6 kt |
| 15 kt | 1.67 → 1.32 s | 36.1 → 28.4° | 0.78 → 0.00 deg/s | 0.0 → 6.8 kt |

### Drift: 3 s of full pedal, speed free under thrust (thrust/weight)
| Entry, T/W | Peak sideslip 1.1.2 → **1.1.3** | Peak yaw rate | Speed after 3 s |
|---|---|---|---|
| 8 kt, 0.15 | 12.3 → **13.0°** | 35.3 → 30.5 | 9.1 → 7.5 kt |
| 15 kt, 0.15 | 6.3 → **10.1°** | 37.1 → 42.6 | 15.4 → 12.2 kt |
| 15 kt, 0.5 | 1.9 → **3.5°** | 28.2 → 33.4 | 36.3 → 33.1 kt |
| 25 kt, 0.5 | 3.9 → **7.6°** | 23.7 → 29.2 | 46.6 → 43.3 kt |
| 40 kt, 0.15 | 1.3 → **5.0°** | 21.3 → 26.5 | 42.0 → 38.7 kt |

- **GroundPedalBrake:** at 1.0, 15 kt gives 12.7° and 8.9 kt after 3 s. At 0.25 or below it is stock, since the stock 0.3 underneath wins.

### Unchanged
- **Take-off bench** (200 km/h rotation; standing start at full throttle, pull at 14 s): summary lines identical to 1.1.2.
- **Two flight cases** (pull-then-roll at 560 km/h, push-then-pull-through + roll at 350 km/h): FCS log identical except the timing columns (µs) and the five new columns.

## Install
- **Main:** `plugins\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` 1.1.3. 1.1.2 kept as `Ifrit_KR67_FCS.dll.1.1.2.bak`.
- **Dev DLL:** unchanged. The new keys are in the dev config, under "Ground".
- **Release:** `C:\Games\NO mods\Ifrit-FCS-1.1.3-release`.
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
  - `bench/sim/GroundYaw.cs` (`gyaw`, with gear options);
  - `bench/stubs/Game.cs` / `Acc.cs` / `Plugin.cs`;
  - `bench/sim/Program.cs`.

Previous notes: `claude/Ifrit_FCS_1.1.2_notes.md`.
