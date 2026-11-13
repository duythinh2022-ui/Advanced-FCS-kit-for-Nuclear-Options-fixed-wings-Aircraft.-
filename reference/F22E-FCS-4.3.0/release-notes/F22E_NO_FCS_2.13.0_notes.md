# F-22E FCS 2.13.0 — without fins, the push-over pace follows the measured shed alone

Base: 2.12.1. **Compiled + bench-checked (clean-jet suite identical — the gate is yawHealth), not flown.** 2.12.1 kept as
`.dll.2.12.1.bak`. Damage case only.

## From the 18:47 flight (2.12.0, both fins shot off at 200 s, then 50–65° AoA)
Corkscrew without fins at 50–65°: |pS| 32 deg/s median against 42 asked (the 2.12.0 fade working; the window holding 81 %),
sideslip p90 5.5°, max 6.9° — fine. Tom: "the stabs and maybe the flaperons are slow to react to stops or reversals" — yes,
they are the only yaw there is now, and the window holds the target while they get there.

Push-overs without fins: released, 130 kt / 28 deg/s → 22.0°; 206 kt / 42 → **33.6°**; 163 kt / 38 → 27.9°; **held roll, 177 kt /
46 → 57.8° (departed)**. Tom: "does the governor account for the fin loss in push-overs?"

It does, but only through the measured capacity, and that reads wrong without fins. 263.8 s: `capZN` toward the shed side
read 24–49 deg/s² while the yaw shed at 10–25 at 65–45° AoA and 5–11 below 40°; the pace ("shed at this rate down to zero,
leave √(2·a·4°) for the rudders at the bottom") allowed 44–51 deg/s of AoA rate, the sweep ran 65 → 5° in 2.4 s with 16
deg/s of yaw left at the crossing and no fin to kill it: 33.6°. Two things are backwards without fins: (1) the shed profile
— the stabs' differential and the flaperons give most of their yaw at high AoA and ~5 deg/s² near zero, the opposite of the
clean jet whose fins come back as the AoA falls, so "this rate down to zero" and "leave some for the rudders" both over-
promise; (2) the estimator's probe re-purposes the roll surfaces for yaw that the real allocation keeps on roll and pitch —
2–4× over-read (the two-axis probe, next). The held-roll push is unrecoverable by any pace: 46 deg/s of rotation fed by the
stick, 5–8 deg/s² of shed; the nose has to stay up until the roll is released.

## Change
With the fins gone (`finK` = yawHealth / 0.5 → 0 with both lost; one fin counts in full), the shed pace uses the measured shed
alone (no 0.4 × capacity floor) and the crossing allowance (`PushoverSideslip`) is scaled to zero: the nose waits at high AoA
while the rotation is shed where the shed exists, and comes through only as the yaw goes. Expect the no-fin released push to
hold 60–65° for ~2–3 s on a 40 deg/s corkscrew (Tom, 2 Nov: smooth over fast in the push-over), and the held-roll push to
hold until the roll is released.

## Bench
Reversal suite, corkscrew entries, regression set, utilisation invariant: identical (fins present).

## Carried
`DamageLatLimit` below the rudder fade, `FinLossAoACap` (Tom had it at 65 for this flight: 65° reached, no AoA issue), the
estimator's no-fin over-read (two-axis probe) — the next build.
