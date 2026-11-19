# F-22E FCS 2.3.1: settled window earlier and faster, stop governor off

Base: 2.3.0. Logs: 21 Oct 22:51 (118 s) and 22:56 (341 s), both 2.3.0. No control-law additions; three defaults and three new Dev keys.

## What the 2.3.0 logs showed (65 deg corkscrew releases, 4 + 18 events; mean / worst)

| | 2.2.29 | 2.3.0 (22:51 / 22:56) | 2.0.5 |
|---|---|---|---|
| Pitch dip after release | -2.6 / -5.1 deg/s | -1.8 / -3.6, -1.5 / -4.2 | -2.4 / -4.2 |
| Sideslip 0.3-2 s after release | 3.6 / 6.6 deg | 2.4 / 3.6, 2.2 / 8.7 | 2.8 / 5.1 |
| Residual roll 2-3 s after release, old direction | 5.2 / 11.6 deg/s | 7.3 / 15.1, 5.2 / 21.0 | 1.0 / 7.3 |
| Roll command still in the old direction 1-3 s | 4.2 / 10.4 | 12.6 / 28.0, 9.6 / 43.4 | 2.0 / 7.3 |

- **The stop governor did what it was built to do and that is the problem.** It cut the sideslip (3.6 -> 2.2 deg) by holding the roll command while the yaw caught up, so the roll ran on with the yaw: the roll command stayed in the old direction 1-3 s after the release (12.6 / 9.6 deg/s against 4.2 on 2.2.29) and the roll with it. 2.0.5 stops the roll in 1.7 s with the sideslip still low because its yaw brake is 30 % stronger (22.5 against 17.5 deg/s^2), not because the roll waits. **Off by default** (`CoordinatedStop` false; a file holding true from 2.3.0 is set to false once, revision 11).
- **The roll run-on at low IAS is the yaw brake's own roll.** 22:51 log, 110.7 s (52 m/s, 64 deg): after the release the body roll accelerated from -10 to -28 deg/s against a -13 command while the ailerons and flaperons sat at their stops the other way: the stab split braking the yaw (+35 / -30) rolls the jet harder than the wing can cancel at that speed. The stop law cannot fix that; it is allocation at the stop (yaw brake against wing roll authority), to be looked at next.
- **Stuck sets, two kinds left.** (1) Above ~40 deg AoA no stab deflection is attached at all (local AoA 29 at +35, 97 at -30), so the window has nothing to walk to; the stab parked at -30 by the yaw brake stays there until the AoA is down to ~40, then the walk started late: the window gate waited for the rates to settle under 4 deg/s while the sideslip correction was still yawing the jet at 5-8 deg/s (22:51 log, 113 s: window on at 2.4 s, stab back at 3.6 s, nozzles 8 -> 14 deg meanwhile). **Gate 4/10 -> 10/20 deg/s, walk 15 -> 30 deg/s** (`SettledRateOn`, `SettledRateOff`, `SettledWalkRate`, Dev "4. Gains"). Bench, replay of that release: nozzle peak 15.7 -> 12.7 deg. (2) Both stabs a few degrees nose-down inside the attached range with the nozzles 10 deg nose-up (22:56 log, 18-24 s): the bench reproduces it and shows the allocator leaving a 2-4 deg/s^2 pitch residual with the nozzles unused at +-20 deg of travel, and the regularisation that should pull the set back to neutral scaled to 0.03 by the low-q relief at 60 m/s. That is a solver / regularisation question, not a window one; not changed here.

## Pitch dip (-1.8 / -1.5 deg/s mean now)
The bench reproduces the dip and shows where it comes from: at the stop the stabs sit on their lift plateau (their modelled pitch moment is -27..-33 deg/s^2 at any deflection from +25 to -14), so pitch rests on the nozzles and the wing; the hold error of -1 to -1.5 deg/s then persists for ~1 s with the nozzles at 0 deg and a 2-4 deg/s^2 residual left by the allocator. Closing that residual (and the same for the hold accuracy you asked for) is the solver question above. Not changed in 2.3.1.

## Regression
- Suite: within rounding of 2.3.0 / 2.2.29.
- Bench replays of eight 2.3.0-log releases: sideslip and roll within 0.5 deg / 1 deg/s of 2.3.0 with the governor off; nozzle peaks in the stuck cases 15.7 -> 12.7, 1.9 -> 1.8, 3.9 -> 3.8.

## Not verified
- Not flown.
- `TvcNeutralHold`, `RollFightReleased` left as they are.

Changed source: Plugin.cs (three keys, CoordinatedStop default and migration), FcsController.cs (gates from Plugin), version strings.
