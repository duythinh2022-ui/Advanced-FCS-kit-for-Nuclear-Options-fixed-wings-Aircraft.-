# F-22E FCS 2.5.15

Base: **2.5.14**. Two things you asked for: no-chase retired, and the sideslip tolerance measured from the airframe itself instead of entered. Bench-only; the probe's output on the bench's Raptor matches the 14:23 pedal flight (nothing inside 16-20 deg at 25-45 deg AoA).

## Changes
1. **No-chase retired** at level 3 with the pace governor (`paceOwnsF`, the same gate that retired the margin cap and the yaw-reserve governor). Bench at blend 0.41 / margin 4: stops worst 5.2 -> 4.1, reversals 8.8 -> 9.3, mean roll 69 -> 66; regressions mostly 0.2-4 deg better (pull-then-roll 250 5.6 -> 4.6, full aft + roll + reversal 500 6.8 -> 2.6, pull-through 450 6.6 -> 4.8), one worse (roll-then-pull-reversal 400 8.8 -> 10.4). A wash; one device fewer. Still acts below level 3 or with the pace governor off.
2. **Sideslip-tolerance probe** (`RollStopSlipProbe`, Dev, default true). Twice a second (`SlipProbePeriod` 0.5) the FCS evaluates the whole airframe - every aero part at the game's per-part law (the FCS's foil tables, the same path the asym-load sweep uses), the surfaces at their present deflection - at the present AoA and a sweep of hypothetical sideslip to 20 deg each way (`SlipProbeStep` 4, `SlipProbeMax` 20), and reads off:
   - *departure*: the sideslip where the airframe's own yaw moment pushes the sideslip open harder than `SlipAuthUse` (0.5) of the rudders' yaw at that sideslip can pull back (`slipDep`);
   - *authority*: the sideslip where the rudders' yaw or the roll surfaces' roll authority has fallen to `SlipAuthFrac` (0.5) of its zero-sideslip value (`slipAuth`);
   - the directional stiffness at small sideslip, for the record (`slipStiff`, deg/s^2 per deg).
   `slipTol` = the smaller; the margin becomes min(`RollStopSideslip`, `SlipTolUse` 0.7 x `slipTol`), filtered 0.5 s. 25 = nothing inside the sweep. The restoring-yaw sign is latched once at |AoA| < 10 from the sweep itself (any conventional airframe is directionally stable there). Cost: ~0.4 ms per probe, 2 Hz. Nothing entered; a different airframe brings its own tables.
   - **Raptor (bench plant), 450 km/h trimmed:** stiffness +2.7 deg/s^2/deg at 5 deg AoA, +1.9 at 20, +0.2 at 39, -0.2 at 44 (the bare airframe is directionally neutral-to-unstable past ~42 deg - but 0.8 deg/s^2 against rudders with tens left, so no departure bound); `slipTol` 25 at every AoA and speed tried (250 / 450 / 700 km/h, 5-46 deg). **The margin is your 4 everywhere, as before.** That agrees with the 14:23 pedal flight: 16 deg of sideslip at 27-45 deg AoA with roll at the asked rate and 8 deg pulled back in a second.
   - The detection path was checked by lowering `SlipAuthUse` to ~0: at 44 deg AoA it then reports a bound at the first step and the margin follows (bench). On a craft whose fins give out or whose body yaws it open at some sideslip, that is where the cap lands, times 0.7.
3. `paceLead` / `paceHold` unchanged; four telemetry columns appended: `slipTol, slipDep, slipAuth, slipStiff`. Dev-unlock DLL version string 2.5.15.

## Bench at your settings (blend 0.41, margin 4, probe on)
Reversal sweep worst / mean 10.4 / 4.8, stops 4.1 / 2.9, mean roll 66; regressions: rolling pulls 4.6 / 7.3 / 6.5, full aft + roll + rev 320 / 500 5.0 / 7.9, roll-then-pull-rev 4.0, push + roll 3.6 / 1.5, push-overs 3.4 / 3.4, pull-throughs 3.2 / 4.4, 65 deg releases 2.0-2.1. `slipTol` 25 in all 18.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.5.15; `BepInEx\plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 2.5.15. Backup `.dll.2.5.14.bak`. Release `C:\Games\NO mods\F22E-FCS-2.5.15-release`. New Dev key `RollStopSlipProbe` (true). LogOutput: `F-22E FCS 2.5.15`.
