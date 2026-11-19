# F-22E FCS 2.31.0 — the released-stop yaw brake no longer drives the nose past zero (the nose wobble)

Base: 2.30.0 (2.27.0 law + air data at the CG). **Compiled + bench-checked (full suite) + replayed from the logged states of the
17:07 and 14:45 flights, not flown.** No tuning value touched, no player or Dev keys. CSV 309 → **310 columns** (`relBrakeTd` last
in the FCS block). 2.30.0 is `.dll.2.30.0.bak`, 2.27.0 `.dll.2.27.0.bak`.

## What Tom flew and said
17:07 log (2.30.0): "a lot more nose wobble now, especially at the end of the log". 17:30, after flying 2.27.0 again: "the wobble is
on .27" — so it is not the CG change; it is older than 2.30.0.

## The cause (the 2.25.0 released-stop yaw brake, `ReleaseFollowYaw`)
Snap roll + pull, both sticks released at 20–25° AoA, 320–380 kt (17:07 log 186.3 / 192.1 / 194.7 / 197.9 s; 14:45 log 231.6 s on
2.27.0). The roll stops in ~0.3 s; the yaw is still at 33–47 deg/s. With the stick released at more than 16° AoA the follow engages
and asks the yaw loop for its whole braking capacity, rCmd = r − capacity / KYaw. At that speed the capacity is 100–150 deg/s², so
the command sits 40–60 deg/s **past zero yaw** until the yaw is nearly gone (195.46 s: r 7, command −52; 2.27.0 at 232.2 s: r 7,
command −45). The rudders (100 deg/s) need ~0.35 s to come back from the stop, so the yaw goes through zero to −23…−30 deg/s; then
the follow lets go in one tick (command −52 → −10, yaw-acceleration target −240 → +204 deg/s²), and on 2.27.0 it even re-engaged
the other way on the back-swing. Sideslip ±9…12° at ~1 Hz.

## The change (`ReleaseBrakeShaped`, inside the brake request; false = 2.30.0 byte for byte)
The brake asks for the deceleration that takes the yaw out by the time the rudders are back at neutral: **min(capacity, |r| / t)**,
t = the rudders' present travel to neutral / their servo rate (at least 0.15 s). The whole capacity while there is yaw for it,
tapering to zero with the yaw, so nothing is left to overshoot when the follow lets go. The released push-over from 65°, where the
yaw is 50–75 deg/s against 40–60 deg/s² of capacity, is untouched (|r| / t stays above the capacity until the very end).
(Tried first and not shipped: braking exactly toward zero yaw — the back-swing went but the early brake was softer and the first
swing grew, 8.5 → 9.8° on 197.9 s.)

## Bench
Replays from the logged states (bench sideslip at mid AoA tracks the game within 1–3° RMS on these):

| event | sideslip peak 2.30.0 → 2.31.0 | peak to peak | yaw back-swing (deg/s) |
|---|---|---|---|
| 17:07 186.3 s | 9.4 → 4.4 | 10.4 → 6.3 | 21.9 → 8.6 |
| 17:07 192.1 s | 7.6 → 4.8 | 13.9 → 8.1 | 25.4 → 25.4 |
| 17:07 194.7 s | 11.9 → 11.9 | 16.5 → 16.6 | 12.0 → 2.5 |
| 17:07 197.9 s | 8.5 → 8.6 | 15.3 → 15.4 | 24.8 → 8.3 |
| 14:45 231.6 s (2.27.0's) | 6.6 → 5.9 | 6.9 → 7.1 | 19.8 → 2.0 |

Released push-overs from 65° (17:07 56.2 / 110.7 s, 14:45 42.7 / 54.6 s): **identical** to 2.30.0. Full suite: identical except
"pullthrough, roll released at zero 350" 4.3 → 4.4°. What stays: the **first** sideslip swing (9–12° on 194.7 / 197.9 s) — the
roll stopping 2–3× faster than the rudders can reverse; that is the rate cap's stop model (handoff §4 a), a separate change.

## What to expect
The same snap releases at 20–25° AoA / 330+ kt: one sideslip swing as the roll stops, then the nose settles instead of swinging
back. Released and held push-overs as 2.30.0. `LogOutput.log` must say 2.31.0; 310 columns.
