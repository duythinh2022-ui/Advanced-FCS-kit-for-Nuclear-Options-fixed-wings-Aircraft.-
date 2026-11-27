# T/A-30 Compass FCS 0.4.2: the main brakes help the ground steering (port of Su-30 0.5.3 + 0.5.4)

Base: 0.4.1. Tom, 12 Nov 07:41: "update the rest to the new differential brakes". The ground code was identical to the Su-30's before its brake work (the ground methods match it line for line, comments aside), so the Su-30 code went in unchanged, as on the F-22E 4.1.5 and Ifrit 1.1.3.

**Changelog:**
- **Turn stop.** After you release the pedal, the brake on the outside main now helps the nose wheel stop a ground turn's yaw. It takes only what the nose tyre can't grip: full brake authority to 15 kt, faded out by 25 kt. On the bench, release stops at 7–17 kt are 15–19 % shorter (10 kt, full pedal: 1.52 → 1.25 s). The cost is 4.1–5.4 kt more speed lost per stop from a full-pedal turn.
- **Light nose.** When the nose goes light mid-stop, the nose wheel keeps its working angle instead of swinging to full opposite lock and then 0.
- **Pedal brake (drift).** While the pedal is in, the main on the pedal's side is braked by |pedal| × 0.5, where stock gives 0.3 and only at 3–30 m/s. The jet pivots harder and drifts.
- **New dev settings:** GroundIndiBrake, GroundIndiBrakeMax (1.0), GroundIndiBrakeFullSpeed / ZeroSpeed (7.7 / 12.9 m/s), GroundIndiLightNoseHold, GroundPedalBrake (0.5).
- **Log columns:** gsBrkL, gsBrkR, gsBsL, gsBsR, gsLn.

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,368,064 bytes, SHA-1 c4c598e8bca3dffae1e0c4a41e963543f59e60b1. The 0.4.1 source rebuilt here gives the released 0.4.1 DLL byte for byte (d5bb421f…).
- Benched on the `gyaw` ground bench, ported from the Su-30, with this jet's gear data. Also on the take-off bench and with flight cases.
- Run on the game's Mono under Wine:
  - gear patch applied (prefix + postfix, `GearSwap` state);
  - native core self-test 200 / 200.
- **Not flown.**

## Gear (`trainer`)
Nose: lock 45°, 60 deg/s, aligning 5, μ 0.8, not braked. Mains: μ 0.8, braked, `differentialBrakeFactor` −1 / +1.

- **Bench geometry:** 7.04 t (the bench airframe's spawn mass), wheelbase 5.40 m, track 2.20 m.
- **Iz 131,000 kg·m²:** the bench airframe's point-mass yaw inertia × 1.63. That factor is what the F-22E's and Ifrit's ground-fitted values are over their own point-mass sums (1.55 / 1.71).
- **Nose load share 20 %, an assumption:** no taxi logs measure it on this jet. The F-22E and Ifrit measured 18 / 24 %; the bench airframe's own CG gives a number about 3× too low on both. The sensitivity is below.

## Bench (`gyaw`; no aero, so nothing above ~60 kt is meant literally)
### Release stop after a 2.5 s pedal turn
Pedal brake off in both columns, so both releases start from the same turn. Stop = yaw rate under 2 deg/s.

| kt, pedal | yaw at release | stop 0.4.1 → **0.4.2** | heading after release | swing-back deg/s | kt lost to the stop | as shipped (pedal brake on): yaw at release / stop |
|---|---|---|---|---|---|---|
| 7, full | 36.0 | 1.35 → **1.10 s** | 25.4 → 21.8° | 1.03 → 0.23 | -1.1 → 3.6 | 37.7 / 1.18 s |
| 7, half | 15.9 | 0.62 → **0.53 s** | 5.2 → 4.9° | 0.37 → 0.09 | 0.1 → 1.5 | 16.1 / 0.53 s |
| 10, full | 41.0 | 1.52 → **1.25 s** | 33.4 → 28.9° | 1.01 → 0.11 | -0.5 → 4.9 | 44.7 / 1.32 s |
| 10, half | 22.6 | 0.82 → **0.68 s** | 9.5 → 8.8° | 0.95 → 0.02 | 0.0 → 2.3 | 22.8 / 0.70 s |
| 13, full | 39.3 | 1.43 → **1.20 s** | 31.5 → 27.5° | 1.03 → 0.11 | -0.0 → 5.1 | 42.3 / 1.27 s |
| 13, half | 29.1 | 0.98 → **0.82 s** | 14.3 → 12.7° | 1.24 → 0.24 | 0.0 → 3.1 | 29.4 / 0.83 s |
| 17, full | 37.3 | 1.28 → **1.08 s** | 26.5 → 23.6° | 1.09 → 0.25 | 0.3 → 4.4 | 40.1 / 1.15 s |
| 17, half | 36.1 | 1.12 → **0.93 s** | 19.8 → 17.8° | 1.18 → 0.26 | 0.0 → 3.4 | 36.9 / 0.95 s |
| 23, full | 33.3 | 1.10 → **1.03 s** | 20.1 → 19.3° | 1.17 → 1.15 | 0.6 → 1.9 | 35.6 / 1.07 s |
| 23, half | 31.5 | 0.98 → **0.93 s** | 16.2 → 15.8° | 1.50 → 0.98 | 0.3 → 1.3 | 32.7 / 0.95 s |

- **All switches off** (GroundIndiBrake / GroundIndiLightNoseHold off, GroundPedalBrake 0): identical to 0.4.1 in all 10 cases.
- **Nose share sensitivity** (10 kt, full pedal, stop 0.4.1 → 0.4.2):

| Nose share | Stop |
|---|---|
| 12 % | 2.00 → 1.38 s |
| 28 % | 1.25 → 1.12 s |

  The lighter the nose, the more the brakes add.

### Light nose: nose load gone 0.3–0.85 s into the release

| Speed | Stop | Heading | Swing-back | Speed lost |
|---|---|---|---|---|
| 10 kt | 1.95 → 1.62 s | 48.1 → 39.4° | 1.01 → 0.07 deg/s | -0.4 → 7.8 kt |
| 15 kt | 1.77 → 1.47 s | 41.5 → 34.3° | 1.22 → 0.08 deg/s | 0.3 → 7.9 kt |

### Drift: 3 s of full pedal, speed free under thrust (thrust/weight)
| Entry, T/W | Peak sideslip 0.4.1 → **0.4.2** | Peak yaw rate | Speed after 3 s |
|---|---|---|---|
| 8 kt, 0.15 | 10.1 → **10.8°** | 41.3 → 34.2 | 8.1 → 6.3 kt |
| 15 kt, 0.15 | 3.9 → **6.9°** | 40.8 → 45.4 | 15.1 → 11.4 kt |
| 15 kt, 0.50 | 4.1 → **5.7°** | 32.2 → 36.9 | 35.8 → 32.4 kt |
| 25 kt, 0.50 | 6.9 → **10.2°** | 27.9 → 32.8 | 46.0 → 42.5 kt |
| 40 kt, 0.15 | 4.2 → **7.9°** | 25.4 → 30.2 | 41.5 → 38.0 kt |

### Unchanged
- **Take-off bench** (200 km/h rotation; standing start at full throttle, pull at 14 s): summary lines identical to 0.4.1.
- **Two flight cases** (pull-then-roll at 560 km/h, push-then-pull-through + roll at 350 km/h): FCS log identical except the timing columns (µs) and the five new columns.

## Install
- **Main:** `plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll` 0.4.2. 0.4.1 kept as `Compass_TA30_FCS.dll.0.4.1.bak`.
- **Dev DLL:** unchanged. The new keys are in the dev config, under "Ground".
- **Release:** `C:\Games\NO mods\Compass-FCS-0.4.2-release`.
- **Mod manager:** update the version there, or its modpack may put the old DLL back. Restart the game.

## Files
- `FcsController.cs`:
  - `GroundSteer`: brake allocation in the release stop and the hold, light-nose hold, pedal brake;
  - `GsIndiWheel` (nose share);
  - new `GsLightNoseWheel`, `GsNoseAlpha`, `GsBrakeAlloc`, `GsPedalBrake`, `MainBrakeTarget`;
  - telemetry.
- `Hooks.cs`: `GearPrefix` / `GearPostfix` write and restore each main's brake (`GearSwap`).
- `Acc.cs`: `LG_braked`, `LG_dbf`, `LG_groundSpeed`, `LG_brakeStrength`.
- `Plugin.cs`: the six dev keys.
- Bench:
  - `bench/sim/GroundYaw.cs` (`gyaw`, with gear options and a parts mass / inertia readout);
  - `bench/stubs/Game.cs` / `Acc.cs`;
  - `bench/sim/Program.cs`.

Previous notes: `claude/Compass_FCS_0.4.1_notes.md`.
