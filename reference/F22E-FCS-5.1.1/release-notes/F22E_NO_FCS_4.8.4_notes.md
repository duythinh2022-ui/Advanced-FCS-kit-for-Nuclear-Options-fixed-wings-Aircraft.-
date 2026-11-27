# F-22E Strike Raptor FCS 4.8.4 (26 Nov 2026)

Ask: Tom 26 Nov 00:20 (fix the 4.8.3 release-sideslip regression) and 00:24 (why not make the FCS inherently robust so stuck / parked stabs never happen). Bench only; not flown.

## Change
`StabBranchSwitch` (a 1.8.21 patch) is no longer used with `TwoZoneAirframe` on. Nothing else changed; X2e is bit-identical to 4.8.3 on the battery.

## Cause of the 4.8.3 regression
- The branch switch watches each stab's local AoA and, when the allocator's own solution puts it more than ~10 deg past the lift peak, pins the stab to a trailing-edge-up target on the "attached" side and re-solves everything else around it.
- It reads one lift peak per stab (the inboard zone's). A two-zone stab has two, 0.84 x body AoA apart: at 30 deg body AoA its "attached" target leaves the outboard half at about 45 deg local, stalled.
- 4.8.3 moved the stab peak 36 -> 33 deg, so the patch's "deep" threshold dropped from 46.7 to 42.8 deg local and it started firing at the 36 deg limit, where 4.8.2 never fired it.
- Bench trace (320 km/h idle, roll at 36 deg, both sticks released): with the patch firing, the left stab wandered between -6 and +11 for 0.9 s instead of going to -30; its drag there is about half the yaw the jet has at that speed (-9 of -20 deg/s^2). Yaw rate fell 28 -> 22 deg/s in the first second (4.8.2 setup: 28 -> 14), the roll stopped without it, sideslip ran to 12 deg, and the yaw-starved allocator then held the left stab at +35 against full nose-down nozzles for 1.2 s.

## Bench, two-zone, 4.8.3 -> 4.8.4
| | 4.8.3 | 4.8.4 |
|---|---|---|
| Release from rolling 36 deg hold, 320 km/h idle, worst sideslip, no TVC / TVC | 11.1 / 12.0 | 7.0 / 6.0 (4.8.2: 8.4 / 5.3) |
| Same, stab-vs-nozzle fight | 1.5 s | 0 s |
| Release grid, 18 cases (280/320/380 km/h x 30/36/42 deg x TVC on/off), worst sideslip mean / max | 7.6 / 12.9 | 5.8 / 8.6 |
| Half-stick roll at 36 deg, sideslip, no TVC | 11.6 | 8.8 |
| Loaded roll from 45 deg, no TVC, deg/s | 105.6 | 111.1 |
| Reversal roll 30/36/45/50, no TVC | 65.0/57.4/56.4/51.9 | same |
| AoA-limit overshoot, pushes, low-AoA battery, 21:58 bleed case | | unchanged |

Three of the 18 release cases are 1.2-1.8 deg worse (6.1 -> 7.3, 4.4 -> 5.8, 4.7 -> 6.5).

Crude signatures over all runs (4.8.3 -> 4.8.4): stab past 20 deg with the nozzles past 12 deg the other way, TVC battery 6.3 -> 4.9 s of 170 s; a stab past 25 deg with both sticks free below 25 deg AoA, 5.0 -> 3.2 s (TVC) and 3.3 -> 2.5 s (no TVC); command reversals over 1 deg per tick 461 -> 436 and 267 -> 236. Not zero. Some of the remaining seconds are poses the two-zone airframe uses on purpose (inside stab trailing-edge down in a high-AoA roll); they have not been separated out.

## Why stuck / parked poses exist at all (00:24 question)
- The allocator (`Allocator.SolveCore`, mirrored in the Rust core) searches every effector over its whole travel, but one effector at a time, starting from last tick's command, with a penalty on moving away from it.
- A stab's force curve has a peak, so the cost has more than one valley. Starting from the last command, the search finds the nearest valley. Crossing to the other one needs the stab and the nozzles to move together, which a one-at-a-time search only finds through the paired search.
- The branch switch, `TwoZoneStabPeakClamp`, and `StabBranchHold` are patches that push it out of a valley from outside.
- An earlier message in this chat said the allocator "only searches the angles each surface can reach in this tick". That was wrong: it is the start point and the one-at-a-time search, not the range.
- The source fix is in the solver: solve from more than one start (last command, and each stab on the other side of its peaks) and keep the cheapest, with the travel time already costed by the two-horizon reach plan. That needs the start point separated from the movement-penalty reference inside `SolveCore` and the same change in the Rust core (new prebuilt DLL), plus a check of the allocator's CPU time. Not done in 4.8.4.
- Making the stab curve single-valued by limiting travel to the near side of the peak is not available on this airframe: above about 27 deg body AoA every trailing-edge-down stab angle has the outboard zone past its peak, so that would remove the stabs' nose-down.
