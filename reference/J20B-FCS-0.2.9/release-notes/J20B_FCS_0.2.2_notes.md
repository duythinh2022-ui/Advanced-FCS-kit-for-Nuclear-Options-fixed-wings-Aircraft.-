# J-20B Weilong FCS 0.2.2: held push-overs, the canard roll option

Base: 0.2.1. **Status:** compiled against the game's assemblies, benched. **Not flown.**

Tom (24 Nov 15:02): "the jet flies well with +55/-45. But release and held pushovers are not well, especially the held one, roll just halts during pushovers and it is not a very smooth ride. Also there was at least 1 occasion that aoa undershoot to 50 from 55 ... And is the canard roll key missing?, i want it back as an option".

## The held push-over: the roll target flickered (fixed)
14:37 log, held push-overs from 55 deg with full roll (446.6 s, 497.4 s): the planned push (`ppOn`) stops a held roll, but the roll target flipped between 0 and the stick's 16-42 deg/s 34-40 times in 1.7 s. The bleed only acted while the roll trajectory was above 0.5 deg/s; once it got there the stick took the roll back, and the bleed took it again the next tick.

**Change (`PlanBleedHoldsStop`, 6. Lateral governor, on):** with the roll stick held, the plan keeps the roll stopped for the whole plan and gives it back below 10 deg AoA, as the law was meant to. false = 0.2.1 (bench identical).

| Bench, pull to 55 with full roll, then full push, roll held | 0.2.1 | 0.2.2 |
|---|---|---|
| 250 km/h: roll-target jumps over 8 deg/s | 35 | 3 |
| same, peak sideslip | 7.0 deg | 6.0 |
| 300 km/h: target jumps | 28 | 2 |
| same, peak sideslip | 7.2 deg | 6.8 |
| Time the roll target sits at zero | 1.2-1.3 s (in pieces) | 1.5-1.9 s (one piece) |

Regression set (20 cases, your config): identical. The change only acts in a planned push with the roll stick held.

## The halt itself: now an option (`PlanStopsHeldRoll`, 6. Lateral governor, default true)
The stop is Revoker 0.7.0's rule (your 5 Nov ask: "stop the roll during that pitchdown"): the yaw that coordinates a held roll is what the push has to shed before zero AoA. `PlanStopsHeldRoll = false` keeps the held roll going, paced by the yaw the plan can shed (the F-22E's held push-over law): no halt, but on the bench 9.3 / 14.1 deg of sideslip against 6.0 / 6.8 at 250 / 300 km/h. Your call; the default stays true.

## The 55 -> 47.6 deg undershoot (318-320 s): not changed
The FCS asked for 26 deg/s of pitch rate and got 12-16, with 20 deg/s^2 of nose-up unmet. Where the authority went: full roll stick at 55 deg and 250-275 km/h had the elevons stop to stop for the roll (left -30, right +35), and the canards were past their lift peak (local AoA 44-47 deg against a 36.6 peak). So the roll took the elevons and the airframe had little left. The bench airframe cannot hold 55 at all (its nose-up runs out at 47-48), so nothing could be tested there. `LateralReserveBacked = true` in the dev config would keep the elevons for pitch (it caps the roll's share at what nozzles can put back, and the J-20B has none) at the cost of roll at the limit; it is untested at 55 because of the bench.

## The canard roll key: it is there
`CanardSymmetricOnly` in `J20B_Weilong.FCS.Dev.cfg`, section `[5. Airframe]` (your file has it at line 1097, = true). false = the canards roll and yaw too (0.1.1-0.1.3 behaviour). Next spawn.
