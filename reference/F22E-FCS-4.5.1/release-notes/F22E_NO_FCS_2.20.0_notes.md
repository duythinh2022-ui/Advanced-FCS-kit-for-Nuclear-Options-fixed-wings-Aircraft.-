# F-22E FCS 2.20.0 — the yaw target may ask the stabs for their yaw; the no-fin stop holds the body roll, not the rotation

Base: 2.19.0. **Compiled + bench-checked, not flown.** Two law changes, both scaled by the fin loss (the clean jet's bench
set is identical to 2.19.0 — reversals, corkscrew entries, regression set, utilisation 4 FAIL / 9 as before; the one-fin
and engine cases unchanged). No tuning values touched. Statics for the A/B: `YawClampStabReach` (+ `YawClampStabFree`
0.25), `PaceStopNoFin`, both on.

## The 00:25 flight (2.19.0, both fins off at 33 s) — "the flaperons are not hesitant any more, the stabs are"
The six reversals at 65°, from the log (t to the yaw's zero crossing after the stick):

| t | stick | r at stick | t to zero | yaw target | yaw unmet | stabs L / R |
|---|---|---|---|---|---|---|
| 67.0 | L | +36 | 2.2 s | 21 | −31 | −2 / +15 |
| 76.5 | R | −31 | **4.35 s** | **9** | **+67** | −3 / +19 |
| 83.2 | L | +15 | 0.85 s | 28 | −5 | −17 / +26 |
| 84.9 | R | −21 | **10.5 s** | **9** | **+51** | −1 / +21 |
| 87.3 | L | −4 | 8.1 s | 25 | −22 | −13 / +24 |
| 89.1 | R | −27 | **6.3 s** | **8** | **+57** | +1 / +24 |

Every right-going reversal: the yaw **target** sat at +8…+9 deg/s² against a demand of ~+80 (unmet 50–67), the flaperons
at ±25 (the 2.2.7 split, `decel` 1) and the stabs idle at −3 / +20 — with 22–27 deg/s² of +yaw in their tables
(`fcsmom` on the log). The left-going ones were quick only because the roll loop's stab differential happened to yaw
the right way.

**The cause is 2.0.12's yaw-target clamp** (`YawTargetClamp`): with a rudder gone or an engine out, the yaw target is
limited to what the *rudders* reach with every other surface where it is (plus, since 2.2.7, the flaperons' drag-yaw
split). The stabilators are not in that reach — right for 2.0.12's case (one fin, mid AoA, stabs on the pitch: the
allocator was swapping a stab ±35 at 0.6 Hz for scraps of yaw), wrong at 60–65° without fins, where the nozzles carry the
pitch and the stabs' differential *is* the yaw axis. The allocator was never asked for the yaw the stabs had. It also
explains the bench: its "no-fin plant sheds 3 deg/s²" (2.16.0–2.19.0 notes) was this clamp, not the plant — with the
stabs in the reach the bench sheds 20–25 deg/s² at 65°, the flight's number, and its no-fin leaf stops and reversals
complete for the first time. (The 2.19.0 replay tool also ignored `detach=` — every "bench escapes the stuck state" replay
in the 00:25 analysis was a jet with fins. Fixed; replays below are without fins.)

## Changes
1. **Stab reach in the yaw-target clamp** (`YawClampStabReach`): while the stabs are off the pitch (`stabPitchFrac` <
   `YawClampStabFree` 0.25, the 2.19.0 nose-hold's criterion, scaled to zero at it) each stab's yaw range from where it
   sits is added to the clamp's reach. On the pitch (below ~60°, or any time the nozzles are not carrying it) the reach is
   2.0.12's. Only acts where the clamp acts: fins damaged, engine out or asymmetric.
2. **No-fin stop** (`PaceStopNoFin`): the window's "never a roll against the one in progress — stop instead" rule set the
   velocity-roll target to 0 while the yaw still carried the rotation. Without fins the yaw sheds at the stabs' pace
   (20–25 deg/s² at 65°) whatever the roll does, and a target of 0 against r = 24 asks the body roll for −r tan α (anti-
   coordinated); the roll loop's try at it was the reversal's sideslip (bench leaf rev 370: p +11 → −10 while r went 24 →
   7, β 23°). Now, scaled by the fin loss, the target may not go below the rotation the jet has with the body roll stopped
   (r sin α, falling with the yaw): p → 0, the nose yaws through, the sideslip grows at r cos α only. Since that target
   gives the yaw loop only ~0.18 r of error, the yaw command is set directly to measured − capacity / `KYaw` during the stop
   (the 2.17.0 lead, on the yaw only). Scaled by the **fin** loss, not the rudder fade: with the fins on above the fade
   the sideslip the old stop builds is what weathercocks the yaw round (held to r sin α the clean 65° reversal at 500 took
   3.1 s instead of 1.6) — the clean jet keeps its reversal.

## Bench, 2.19.0 → 2.20.0 (no-fin numbers are now at the flight's shed rate; sideslip still ~2× high)
Clean set, one fin, engine out, fins + flaperon: identical. No fins: leaf stop 296 / 370 **9.9 / 13.3 → 3.8 / 5.7°**
(complete in 2.0 / 2.2 s; never completed before), leaf rev **12.9 / 11.5 → 5.9 / 8.2°** (2.75 / 3.2 s; never before);
push-overs 11.5 / 11.5 / 9.9 / 9.9 → 20.2 / 20.2 / 12.9 / 12.9° — the rotation on at the push is now 84 / 81 deg/s where
it was 64 / 51 (the stabs build the rotation as well as brake it); none depart, the nose-hold holds (held: 57–60°).
Replays of the 00:25 log's four slow reversals from their logged states, no fins, 2.19.0 laws → 2.20.0: 76.6 s 2.9 → 2.3 s
(β 13.7 → 4.7), 79.5 s 1.1 → 0.7 (10.8 → 10.4), 85.0 s never → 1.3 (17.8 → 7.1), 89.2 s 2.6 → 1.7 (11.5 → 8.0) — the game
took 4.35 / 1.35 / never / 6.3.

## What to expect without fins
Both stabs go to the stops together the moment the stick reverses, either direction; the body roll stops, the nose yaws
through in 1–2 s, the roll builds the other way as the yaw crosses. Stops the same. Push-overs as 2.19.0 (nose waits at
60–65°; the rotation at the push sets the sideslip). With one fin: half of each. Clean jet: nothing.

## For the next log
Both fins off: reversals and stops at 65° both directions (right-going should now match left-going), a held and a released
push. One clean 65° reversal to confirm nothing moved. `LogOutput.log` must say 2.20.0.
