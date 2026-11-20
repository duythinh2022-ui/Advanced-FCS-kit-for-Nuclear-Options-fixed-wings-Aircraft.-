# FS-20 Vortex FCS 0.2.7 (17 Nov 2026)

Base: 0.2.6. **Status: compiled and benched. Not flown.**

## Fore-aft stop: "feels like a brake that slowly ramps up"

- **Why.** In the hover the nozzle tops out near vertical, so stopping from 30 kt needs the nose up: about 20 deg at 11.9 t.
  That nose-up was built in three slow steps:
  - The hover's fore-aft tilt was an integral of the residual force: 6 deg/s at most, -6..+10 deg, with a 3 s leak.
  - It was added to the attitude reference before that reference's slew limit, which is 1.5 deg/s nose-up above 10 m/s.
    That limit is meant for the wing's AoA coming in during a transition.
  - The speed loop was 0.6 x the speed error, fading out over the last few m/s.
- **Bench, 30 kt -> 0 at 0.2.6:**
  - The pitch rose at 1.5 deg/s and reached 13 deg after ~6 s.
  - Under 1 kt took 8.8 s.
  - The leak then left the nose up and the jet drifting back at ~1 kt for 10 s or more.

## Fixes (stop on release, as fast as the lift allows, on every axis)

- **Fore-aft tilt worked out directly.**
  - The nozzle takes what it can: its force direction in the body, from swivel 1 to swivel 0.
  - The pitch takes the rest: theta = nozzle limit - atan(a_fore-aft / g).
  - It is bounded by the tilt the translation budget allows, atan(a_h / g), and moves at StovlTrcBankRate, the same rate as
    the sideways tilt.
  - It is added after the attitude reference's slew, so the 1.5 deg/s wing limit no longer holds it back.
  - It follows the command both ways, so the nose comes down as the stop ends. No integral, no leak.
- **Fore-aft speed loop: the same start / stop profile as the sideways one** (0.2.6). Full acceleration until the pitch has
  to come out, leaving room for the pitch loop's ~0.5 s, then a linear end.
- **Hover position hold:** the spot is caught where the jet will come to rest, not where it is. It was caught at up to
  3 m/s, so the jet ran ~2 m past and flew back at ~1 kt.
- **The translation no longer also shrinks on the sink itself** (vPrio). The height loop's demand is already taken out of
  the budget (0.2.6); with both, a 30 kt stop's pitch went 17 -> 11 -> 19 deg as a 0.4 m/s sink came and went.

## Bench

| case | 0.2.6 | 0.2.7 |
|---|---|---|
| 30 kt -> 0 (lever to the aft detent) | pitch 13 deg after ~6 s; under 1 kt at 8.8 s; then ~1 kt back drift for 10+ s | pitch 18-20 deg within 2 s; under 1 kt at 6.2 s; at most 0.5 kt the other way after; vertical speed -0.36..+0.30 m/s |
| 0 -> 30 kt | 27 kt at 5.6 s | 27 kt at 5.2 s |
| full side stick + 30 kt together, then both released: both under 1 m/s | 9.5 s | 7.8 s; vertical speed -0.62..+0.29 m/s |
| side stop with full climb stick in the middle | under 1 m/s at 55.4 s | 55.3 s (bank out while climbing, as 0.2.6) |
| 4 / 10+8 / 15 m/s skids | 2.3 / 5.0 / 5.9 s | 2.3 / 5.1 / 5.7 s |
| yaw 77 deg at ~25 kt; 90 deg pedal at 30 kt | 10.3 s; 6.5 s | 9.9 s; 6.9 s |
| 00:24 bank repro | -0.50..+0.76 m/s | -0.86..+0.45 m/s, 2.1 m lost (see below) |

- **The bank repro got a bit worse.** The fore-aft loop now accelerates harder, so this run sat at ~40 kt, in the
  flight-path regime. There the bank may use all the spare lift (31-33 deg, was 27), and the worst point is the full-stick
  roll reversal. 0.2.4 crashed in the same run.
- **Regression:**
  - Hover pedal / crab at four weights: vertical speed -0.50..+0.32 m/s.
  - Touchdowns -5.0..-5.7 m/s, gear at most 0.402 m.
  - VTO release: no dip.
  - Backwards 30 kt.
  - Mission run: bank at most 12 deg. The decel to the hover now flies up to 20 deg of pitch, the tilt the budget allows.
