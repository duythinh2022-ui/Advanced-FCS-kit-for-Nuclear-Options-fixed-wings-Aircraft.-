# FS-3 Ternion FCS 0.3.2: the main brakes help the ground steering (port of Su-30 0.5.3 + 0.5.4)

Base: 0.3.1. Tom, 12 Nov 07:41: "update the rest to the new differential brakes". The ground code was identical to the Su-30's before its brake work (the ground methods match it line for line, comments aside), so the Su-30 code went in unchanged, as on the F-22E 4.1.5 and Ifrit 1.1.3.

**Changelog:**
- **Turn stop.** After you release the pedal, the brake on the outside main now helps the nose wheel stop a ground turn's yaw. It takes only what the nose tyre can't grip: full brake authority to 15 kt, faded out by 25 kt. On the bench, release stops at 7–17 kt are 11–15 % shorter (10 kt, full pedal: 1.68 → 1.48 s). The cost is 5.4–6.7 kt more speed lost per stop from a full-pedal turn.
- **Light nose.** When the nose goes light mid-stop, the nose wheel keeps its working angle instead of swinging to full opposite lock and then 0.
- **Pedal brake (drift).** While the pedal is in, the main on the pedal's side is braked by |pedal| × 0.5, where stock gives 0.3 and only at 3–30 m/s. The jet pivots harder and drifts.
- **New dev settings:** GroundIndiBrake, GroundIndiBrakeMax (1.0), GroundIndiBrakeFullSpeed / ZeroSpeed (7.7 / 12.9 m/s), GroundIndiLightNoseHold, GroundPedalBrake (0.5).
- **Log columns:** gsBrkL, gsBrkR, gsBsL, gsBsR, gsLn.

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,403,904 bytes, SHA-1 b065bd9fe12592af44a34633545852125536c2c1. The 0.3.1 source rebuilt here gives the released 0.3.1 DLL byte for byte (477149eb…).
- Benched on the `gyaw` ground bench, ported from the Su-30, with this jet's gear data. Also on the take-off bench and with flight cases.
- Run on the game's Mono under Wine:
  - gear patch applied (prefix + postfix, `GearSwap` state);
  - native core self-test 200 / 200.
- **Not flown.**

## Gear (`P_Trisurface1 (blueprint `FS-3.Ternion_1.0.0.nobp`)`)
Nose: lock **60°**, 60 deg/s, aligning 5, μ 0.8, not braked. Mains: μ 0.8, braked, `differentialBrakeFactor` −1 / +1.

- **Bench geometry:** 25.0 t (the bench airframe's spawn mass), wheelbase 9.22 m, track 2.63 m.
- **Iz 1,140,000 kg·m²:** the bench airframe's point-mass yaw inertia × 1.63. That factor is what the F-22E's and Ifrit's ground-fitted values are over their own point-mass sums (1.55 / 1.71).
- **Nose load share 20 %, an assumption:** no taxi logs measure it on this jet. The F-22E and Ifrit measured 18 / 24 %; the bench airframe's own CG gives a number about 3× too low on both. The sensitivity is below.

## Bench (`gyaw`; no aero, so nothing above ~60 kt is meant literally)
### Release stop after a 2.5 s pedal turn
Pedal brake off in both columns, so both releases start from the same turn. Stop = yaw rate under 2 deg/s.

| kt, pedal | yaw at release | stop 0.3.1 → **0.3.2** | heading after release | swing-back deg/s | kt lost to the stop | as shipped (pedal brake on): yaw at release / stop |
|---|---|---|---|---|---|---|
| 7, full | 26.8 | 1.65 → **1.43 s** | 25.6 → 22.9° | 0.81 → 0.02 | -1.6 → 4.7 | 29.9 / 1.63 s |
| 7, half | 12.9 | 0.70 → **0.62 s** | 4.9 → 4.8° | 0.57 → 0.04 | -0.1 → 1.8 | 13.0 / 0.63 s |
| 10, full | 27.8 | 1.68 → **1.48 s** | 28.0 → 25.1° | 0.84 → 0.11 | -0.7 → 6.0 | 29.6 / 1.57 s |
| 10, half | 18.3 | 0.97 → **0.83 s** | 9.3 → 8.6° | 0.85 → 0.21 | -0.2 → 2.9 | 18.4 / 0.83 s |
| 13, full | 28.6 | 1.60 → **1.40 s** | 26.4 → 23.7° | 0.84 → 0.13 | -0.4 → 5.9 | 30.2 / 1.47 s |
| 13, half | 23.5 | 1.17 → **1.00 s** | 14.2 → 12.7° | 0.83 → 0.58 | -0.3 → 3.8 | 23.6 / 1.02 s |
| 17, full | 28.2 | 1.48 → **1.30 s** | 23.8 → 21.5° | 0.96 → 0.43 | -0.0 → 5.4 | 29.8 / 1.37 s |
| 17, half | 27.6 | 1.30 → **1.15 s** | 18.6 → 16.9° | 0.87 → 0.89 | -0.3 → 4.3 | 28.5 / 1.17 s |
| 23, full | 26.5 | 1.30 → **1.25 s** | 19.3 → 18.7° | 0.99 → 0.92 | 0.3 → 2.0 | 27.9 / 1.28 s |
| 23, half | 25.4 | 1.18 → **1.15 s** | 16.3 → 15.9° | 0.87 → 0.95 | 0.1 → 1.4 | 26.1 / 1.17 s |

- **All switches off** (GroundIndiBrake / GroundIndiLightNoseHold off, GroundPedalBrake 0): identical to 0.3.1 in all 10 cases.
- **Nose share sensitivity** (10 kt, full pedal, stop 0.3.1 → 0.3.2):

| Nose share | Stop |
|---|---|
| 12 % | 2.08 → 1.60 s |
| 28 % | 1.47 → 1.37 s |

  The lighter the nose, the more the brakes add.

### Light nose: nose load gone 0.3–0.85 s into the release

| Speed | Stop | Heading | Swing-back | Speed lost |
|---|---|---|---|---|
| 10 kt | 2.15 → 1.97 s | 40.1 → 35.1° | 0.64 → 0.01 deg/s | -0.6 → 7.9 kt |
| 15 kt | 1.97 → 1.73 s | 35.8 → 31.4° | 0.93 → 0.07 deg/s | -0.1 → 8.9 kt |

### Drift: 3 s of full pedal, speed free under thrust (thrust/weight)
| Entry, T/W | Peak sideslip 0.3.1 → **0.3.2** | Peak yaw rate | Speed after 3 s |
|---|---|---|---|
| 8 kt, 0.15 | 13.9 → **18.8°** | 28.9 → 26.1 | 7.3 → 5.0 kt |
| 15 kt, 0.15 | 6.2 → **9.5°** | 30.7 → 32.2 | 14.8 → 11.1 kt |
| 15 kt, 0.50 | 1.8 → **2.0°** | 25.6 → 28.0 | 36.0 → 32.5 kt |
| 25 kt, 0.50 | 1.5 → **2.2°** | 22.1 → 24.5 | 46.5 → 43.2 kt |
| 40 kt, 0.15 | 0.4 → **0.5°** | 21.0 → 23.1 | 41.8 → 38.5 kt |

### Unchanged
- **Take-off bench** (200 km/h rotation; standing start at full throttle, pull at 14 s): summary lines identical to 0.3.1.
- **Two flight cases** (pull-then-roll at 560 km/h, push-then-pull-through + roll at 350 km/h): FCS log identical except the timing columns (µs) and the five new columns.

## Install
- **Main:** `plugins\Ternion_FS3.FCS\Ternion_FS3_FCS.dll` 0.3.2. 0.3.1 kept as `Ternion_FS3_FCS.dll.0.3.1.bak`.
- **Dev DLL:** unchanged. The new keys are in the dev config, under "Ground".
- **Release:** `C:\Games\NO mods\Ternion-FCS-0.3.2-release`.
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

Previous notes: `claude/Ternion_FCS_0.3.1_notes.md`.
