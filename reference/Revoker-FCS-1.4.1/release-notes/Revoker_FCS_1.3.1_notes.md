# FS-12 Revoker FCS 1.3.1: the main brakes help the ground steering (port of Su-30 0.5.3 + 0.5.4)

Base: 1.3.0. Tom, 12 Nov 07:41: "update the rest to the new differential brakes". The ground code was identical to the Su-30's before its brake work (the ground methods match it line for line, comments aside), so the Su-30 code went in unchanged, as on the F-22E 4.1.5 and Ifrit 1.1.3.

**Changelog:**
- **Turn stop.** After you release the pedal, the brake on the outside main now helps the nose wheel stop a ground turn's yaw. It takes only what the nose tyre can't grip: full brake authority to 15 kt, faded out by 25 kt. On the bench, release stops at 7–17 kt are 8–15 % shorter (10 kt, full pedal: 1.00 → 0.87 s). The cost is 2.7–4.0 kt more speed lost per stop from a full-pedal turn.
- **Light nose.** When the nose goes light mid-stop, the nose wheel keeps its working angle instead of swinging to full opposite lock and then 0.
- **Pedal brake (drift).** While the pedal is in, the main on the pedal's side is braked by |pedal| × 0.5, where stock gives 0.3 and only at 3–30 m/s. The jet pivots harder and drifts.
- **New dev settings:** GroundIndiBrake, GroundIndiBrakeMax (1.0), GroundIndiBrakeFullSpeed / ZeroSpeed (7.7 / 12.9 m/s), GroundIndiLightNoseHold, GroundPedalBrake (0.5).
- **Log columns:** gsBrkL, gsBrkR, gsBsL, gsBsR, gsLn.

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,347,072 bytes, SHA-1 df458287c05b8773441561b9e35a8e23bb7d1180. The 1.3.0 source rebuilt here gives the released 1.3.0 DLL byte for byte (f87d9128…).
- Benched on the `gyaw` ground bench, ported from the Su-30, with this jet's gear data. Also on the take-off bench and with flight cases.
- Run on the game's Mono under Wine:
  - gear patch applied (prefix + postfix, `GearSwap` state);
  - native core self-test 200 / 200.
- **Not flown.**

## Gear (`Fighter1`)
Nose: lock 45°, 60 deg/s, aligning 5, μ 0.85, not braked. Mains: μ 0.8, braked, `differentialBrakeFactor` −1 / +1.

- **Bench geometry:** 12.2 t (the calibrated bench spawn mass), wheelbase 7.80 m, track 3.59 m.
- **Iz 232,000 kg·m²:** the bench airframe's point-mass yaw inertia × 1.63. That factor is what the F-22E's and Ifrit's ground-fitted values are over their own point-mass sums (1.55 / 1.71).
- **Nose load share 20 %, an assumption:** no taxi logs measure it on this jet. The F-22E and Ifrit measured 18 / 24 %; the bench airframe's own CG gives a number about 3× too low on both. The sensitivity is below.

## Bench (`gyaw`; no aero, so nothing above ~60 kt is meant literally)
### Release stop after a 2.5 s pedal turn
Pedal brake off in both columns, so both releases start from the same turn. Stop = yaw rate under 2 deg/s.

| kt, pedal | yaw at release | stop 1.3.0 → **1.3.1** | heading after release | swing-back deg/s | kt lost to the stop | as shipped (pedal brake on): yaw at release / stop |
|---|---|---|---|---|---|---|
| 7, full | 25.4 | 0.83 → **0.73 s** | 11.5 → 9.9° | 0.01 → 0.13 | -0.7 → 2.0 | 26.7 / 0.77 s |
| 7, half | 11.1 | 0.40 → **0.37 s** | 2.7 → 2.8° | 0.04 → 0.00 | 0.1 → 0.7 | 11.2 / 0.37 s |
| 10, full | 36.6 | 1.00 → **0.87 s** | 19.1 → 17.2° | 0.21 → 0.00 | -0.6 → 2.8 | 37.3 / 0.90 s |
| 10, half | 15.8 | 0.47 → **0.43 s** | 4.1 → 4.3° | 0.31 → 0.00 | 0.1 → 1.0 | 15.9 / 0.43 s |
| 13, full | 45.8 | 1.12 → **0.95 s** | 26.1 → 23.1° | 0.44 → 0.00 | -0.8 → 3.2 | 47.1 / 0.98 s |
| 13, half | 20.4 | 0.55 → **0.48 s** | 6.0 → 5.9° | 0.43 → 0.00 | 0.1 → 1.2 | 20.6 / 0.50 s |
| 17, full | 41.8 | 1.05 → **0.90 s** | 24.2 → 21.9° | 0.51 → 0.00 | -0.1 → 3.1 | 44.9 / 0.93 s |
| 17, half | 26.4 | 0.65 → **0.58 s** | 8.9 → 8.5° | 0.51 → 0.02 | 0.0 → 1.5 | 26.7 / 0.58 s |
| 23, full | 37.6 | 0.90 → **0.85 s** | 18.8 → 18.3° | 0.62 → 0.51 | 0.3 → 1.2 | 41.4 / 0.87 s |
| 23, half | 35.2 | 0.75 → **0.72 s** | 13.0 → 12.8° | 0.67 → 0.53 | 0.0 → 0.6 | 35.7 / 0.73 s |

- **All switches off** (GroundIndiBrake / GroundIndiLightNoseHold off, GroundPedalBrake 0): identical to 1.3.0 in all 10 cases.
- **Nose share sensitivity** (10 kt, full pedal, stop 1.3.0 → 1.3.1):

| Nose share | Stop |
|---|---|
| 12 % | 1.50 → 1.07 s |
| 28 % | 0.88 → 0.82 s |

  The lighter the nose, the more the brakes add.

### Light nose: nose load gone 0.3–0.85 s into the release

| Speed | Stop | Heading | Swing-back | Speed lost |
|---|---|---|---|---|
| 10 kt | 1.47 → 1.25 s | 30.6 → 23.2° | 0.17 → 0.14 deg/s | -0.5 → 6.2 kt |
| 15 kt | 1.50 → 1.25 s | 37.5 → 30.4° | 0.40 → 0.00 deg/s | -0.2 → 6.4 kt |

### Drift: 3 s of full pedal, speed free under thrust (thrust/weight)
| Entry, T/W | Peak sideslip 1.3.0 → **1.3.1** | Peak yaw rate | Speed after 3 s |
|---|---|---|---|
| 8 kt, 0.15 | 10.3 → **10.8°** | 35.1 → 29.1 | 9.7 → 7.7 kt |
| 15 kt, 0.15 | 7.1 → **9.3°** | 44.9 → 46.1 | 15.0 → 12.6 kt |
| 15 kt, 0.50 | 9.0 → **12.4°** | 40.0 → 46.4 | 34.8 → 31.3 kt |
| 25 kt, 0.50 | 15.0 → **20.5°** | 35.1 → 40.5 | 45.0 → 40.9 kt |
| 40 kt, 0.15 | 13.0 → **22.1°** | 33.5 → 39.5 | 40.1 → 36.0 kt |

- **GroundPedalBrake** 0.25: 15 kt 7.2°, 25 kt 15.3°, 40 kt 13.2° (= stock; the stock 0.3 underneath wins).

### Unchanged
- **Take-off bench** (200 km/h rotation; standing start at full throttle, pull at 14 s): summary lines identical to 1.3.0.
- **Two flight cases** (pull-then-roll at 560 km/h, push-then-pull-through + roll at 350 km/h): FCS log identical except the timing columns (µs) and the five new columns.

## Install
- **Main:** `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` 1.3.1. 1.3.0 kept as `Revoker_FS12_FCS.dll.1.3.0.bak`.
- **Dev DLL:** unchanged. The new keys are in the dev config, under "Ground".
- **Release:** `C:\Games\NO mods\Revoker-FCS-1.3.1-release`.
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

Previous notes: `claude/Revoker_FCS_1.3.0_notes.md`.
