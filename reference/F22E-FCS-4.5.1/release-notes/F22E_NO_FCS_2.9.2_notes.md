# F-22E FCS 2.9.2 — the pitch hold's stabilators are kept inside their attached window

Base: 2.9.1. **Compiled + bench-checked (regression set within ±0.2° on 17 of 18, one 1.2° better), not flown.** 2.9.1 kept as
`.dll.2.9.1.bak`. One new gate on an existing device (`SweepHoldStallWindow`), no tuning values touched.

## From the 16:37 flight (2.9.1)
Released-roll push-overs from 65°: 237 kt / 59 deg/s of yaw → 5.8°; 216 kt / 76 → 11.4°; 215 kt / 75 → **16.8°** (53.7 s;
2.9.0 made 20.6° from 78 at 209 kt). Tom: the hitch is much less sharp but still there.

Why the hold still lost 37 → 22° AoA at −24 deg/s against +5…+10 commanded (54.7–55.0 s), with the weights (pitch 8 / roll
0.5) and the 2.9.1 rate bound both on: the right stab had been parked at −30 by the roll stop through 65 → 50° (pitch was
TVC-only there, `stabPitchFrac` 0), which is **45° of local AoA** — deep past its lift peak. When the hold came, the honest
stab model (correctly) reads trailing-edge-up from there as *un-stalling* the surface: more tail lift, a nose-down increment.
So the allocator had no nose-up gradient on it, drove it further TE-down (cmd −25) for the roll, and put the whole nose-up on
the nozzles (+20, the stop). The nose-up arrived only once the stab had been walked through to the attached side (55.1 s, 0.5 s
later), by which time the AoA was 22° with 43 deg/s of yaw still on. The nozzles then sat at their nose-up stop when the cap
let go at 18°, and took 0.8 s to reverse: that is the remaining hitch (q +10 for 0.3 s at 18°, then −60).

## Change
`SweepHoldStallWindow`: during the capped sweep toward zero AoA (`yawSatCapOn`, the cap clipping the toward-zero side), once the
stabs have pitch range back (`stabPitchFrac` > 0.05, ~55° AoA), both stabilators are held inside their attached window —
local AoA ≤ lift peak + `RpMainStallMargin` 3° — by the 1.8.40 `StallWindow` (it leaves the window open where no attached
deflection exists, i.e. at 65°). The parked stab starts back toward the attached side at ~55° instead of ~40°, so the hold has
gradient on both stabs the moment it needs them and the nozzles are not alone; the roll stop keeps what the stabs give inside
the window plus the flaperons, ailerons, rudders and nozzles. Expected on the 53.7 s push: the hold holding at 40–45° instead
of folding to 22°, and a smaller nozzle back-swing at the release. Not verifiable on the bench (it never makes the rotation).

Telemetry: `sweepWin` (0/1) after `pitchNeedDn`.

## Bench (sideslip ~2× high)
Reversal suite, corkscrew entries, utilisation invariant: identical. Regression set: the window engages in the pushes under a
roll at low AoA — push+roll 300 3.0 → 3.2, pull-then-pushover 350 4.5 → 4.6, **450 7.5 → 6.3**, push-then-pullthrough 350 /
450 unchanged / 8.3 → 8.2, pullthrough released 6.8 → 6.8.

## The guard at 40 (Tom: "flew just fine")
In this log the `SideslipGuardStart` change at ~285 s made no measurable difference to the 22–34° AoA rolling: |pS| 70 → 71
deg/s median, sideslip median 2.3 → 2.2°, p90 5.7 → 6.4°, max 7.6 → 7.8°. The guard was nearly idle there in this flight
(median 0.98 before the change) where it had read 0.74 in the 15:22 log — the 2.9.x pitch side changed how those rolls are
flown. It stays a candidate to retire once the governor holds the margin in the pinned-rudder reversals too; the 2.8.x logs
show it still working there.
