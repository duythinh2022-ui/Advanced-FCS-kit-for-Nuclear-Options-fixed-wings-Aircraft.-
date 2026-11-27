# FS-12 Revoker FCS 1.8.2: pull-to-limit overshoot with TVC off, Typhoon inner flaperons

Base: 1.8.1. **Status:** compiled against the game's assemblies; the law change is benched on replays of the 14:33 flight; the Typhoon change is compiled only. **Not flown.**

Tom (24 Nov 14:32-14:37): "fix the 2 not fixed", "the inner flaperons pair with revoker and typhoon skin is not fixed", "when i roll in the log, aoa dips, and in at least 1 occasion aoa overshoots to 38 despite i had the limit at 35".

## What the 14:33 log (1.8.1, TVC off, 35 deg limit) shows
- **Rolling at the held limit holds:** 40-48 s, full roll for 8 s, AoA 34.8-35.2 deg. The 1.8.0 fix works in flight.
- **Pull to the limit overshoots when the pull follows a fast roll:** 43.1 deg at 32 s, 40.0 at 69.6 s, 37.9 at 88.5 s. Pulls with no roll before them (37 s, 82 s) do not overshoot.
- **Stopping a roll at the limit dips the AoA:** 35.0 -> 29.8 deg at 96.3 s, 34.6 -> 32.0 at 93.0 s. Not fixed, see below.

## Change 1: the surfaces' travel time is part of the stopping distance (`CapSurfaceLag`, 2. Envelope, on)
The pitch rate allowed toward the AoA limit is the one the jet can still stop: q lag + q^2 / 2a. The lag was the nozzles' alone (`CapLagT` x their share of the pitch authority). With TVC off that share is zero, so the stop was planned as if the surfaces were already where the brake needs them.

At 31 s the pull ran at 56 deg/s with the canards at -40 deg (nose-up side). The brake needed them at +50: 0.9 s at the servo rate. The pitch rate stayed at 50-59 deg/s for 0.4 s after the command began to fall.

The surfaces' share of the lag is now the time their servos need from where they are to the deflection that brakes hardest (weighted by the pitch moment each still has to add, halved because the moment comes in along the travel). No new constant. false = 1.8.1 (replay identical, 396 columns).

| Replay of the 14:33 flight | Game | 1.8.1 replay | 1.8.2 replay |
|---|---|---|---|
| 30.2-33.6 s, peak AoA | 43.1 deg | 40.3 | **34.4** |
| same, lowest AoA after the peak | 31.4 | 30.3 | 34.2 |
| 68.0-71.0 s, peak | 40.0 | 35.6 | 34.4 |
| 87.0-90.0 s, peak | 37.9 | 34.5 | 33.7 |

The replay reproduces the first overshoot well and the other two only partly, so only the first is a real test.

**Cost:** a plain full-aft pull is a little slower into the limit. Stick to 34 deg: 1.92 -> 1.98 s at 360 km/h, 1.75 -> 1.92 s at 440 km/h (peak pitch rate 45 -> 40 and 54 -> 46 deg/s).

**Regression set:** TVC on unchanged (mean sideslip 2.29 -> 2.29 deg). TVC off: 3.70 -> 3.83 deg; full aft + roll + reversal 320: 4.2 -> 5.7; pull-through + roll 350: 5.5 -> 4.9; roll-then-pull reversal 400 no longer passes the 45 deg limit (46.1 -> 44.4).

## Change 2: Typhoon inner flaperons
The 14:33 log shows 1.7.1's fix never took: both panels were still bound to `aileron_L/R_visible`. The Typhoon mod finds its driving parts through `ControlSurface.visibleMesh`, and the Revoker's inner flaps are not ControlSurfaces (the high-lift device moves them), so at run time the mod's inner flaperon lists the aileron alone and there was no second candidate to move to.

Now, where two panels took the same surface, the Revoker's own `flap_*_visible` on that side is a candidate too, and the panels on one wing keep the spanwise order of the surfaces they follow. The log line now prints each panel's candidates and span centres. **Compiled only; I cannot see the game.** Expect in LogOutput.log: `Typhoon_flaperon_in_L ... <- flap_L_visible`.

## Not fixed
- **AoA dip when a roll is stopped at the limit (96.3 s: 35.0 -> 29.8 deg).** In the game the allocator splits the canards for the roll stop (right +54 deg, left -23 deg, 50 deg/s^2 of roll and 55 of yaw unmet); they stop carrying the pitch and the pitch rate falls 16 -> 1 deg/s against a command of 13-26. The bench replay of that window does not do it (canards -17 / +28, AoA 34.2), so there was nothing to test a change against.
- **Canards crossing their stall in TVC-off rolls (the twitch).** Same mechanism. Tried this pass: a higher stall price (`StallCostK` 0.03 -> 0.3 / 1.0: command jumps 49 -> 30 / 11, travel 43 -> 38 deg/s, but TVC-off sideslip in pushovers and pull-throughs up by 1-2 deg), and `ReachHeldStick` without the two-horizon pick (travel 38, pushovers still worse). Neither is in. Part of the earlier `ReachHeldStick` pushover figure (4.2 -> 8.1 at 350) was the planned push switching off at its 20 deg entry threshold (20.9 -> 19.9 deg); like for like it is 5.9 -> 8.1 without the plan and 4.2 -> 4.4 with it.
- **Ifrit / MTD 70 -> 68 deg.** Not worked on.
