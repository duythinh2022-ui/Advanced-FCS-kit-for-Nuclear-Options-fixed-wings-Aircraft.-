# FS-3 Ternion FCS 0.2.3: the negative AoA limit no longer depends on where the jet is flying

Base: 0.2.2. One change, in the asymmetric-load sweep. Found in Tom's 11 Nov 11:33 flight (55 % fuel, the Piledriver on one side).

**Changelog:** With an asymmetric load, the negative AoA limit is now the same whether the jet is pulling or pushing. In 0.2.2 it read -18 to -20° during a pull and -40° during a push at the same CG offset and speed, and widened over about 2.5 s through every pushover.

**Status:** compiled against the game's assemblies and benched. **Not flown.**

## What was wrong
- **Cause:** 0.2.2 ran the coupled check (the LP, the optimiser that finds how much roll is left with pitch held) on the negative AoA points only while the present AoA was below 5°, to save solves. Above 5° the negative points fell back to the old one-axis demand, which reads much tighter.
- **Not the flaps.** My first reading of the log blamed the manoeuvre flaps' droop, because the limit moved in step with them. That was wrong: the flaps and the 5° test both follow the AoA. On the bench the limit did not move with the slat-schedule switch or the other coupled-check switches, and did with this one.
- **In the log (34–47 s, CG offset 26 → 20 cm, 85–98 m/s):** negative limit -18 to -20° while pulling, -40° while pushing; -20 → -38° over 43.5–46.5 s during the pushover.

## The change
- `CplNegAlways` (bench switch, default on): the coupled check runs on the negative points at every present AoA. Off = 0.2.2, identical on all 1,141 ticks of the 11:33 replay.

## Bench
**11:33 log replayed, 33–52 s (store layout fitted to the log):**
| | negative limit through the pull and the pushover | lowest AoA | sideslip max |
|---|---|---|---|
| 0.2.2 | -40 → -17 (pull) → -40 over ~3 s (push) | -39.0° | 1.68° |
| 0.2.3 | -40 throughout | -39.4° | 1.68° |

**New negative-AoA set with a store on the left wing (`bench/an/negload.sh`, 50 runs):** 770–1,800 kg (12–28 cm of CG offset). Full forward stick from 220–340 km/h with a full roll and a reversal each way; and a 4 s full pull followed at once by full forward stick with full roll, from 380–520 km/h.
| mean / worst | 0.2.2 | 0.2.3 |
|---|---|---|
| lowest AoA | -25.1 / -40.9° | -26.2 / -41.0° |
| AoA past the negative limit | — / 0.9° | — / 1.0° |
| sideslip max | 4.1 / 5.3° | 3.9 / 5.3° |
| seconds above 5° sideslip | 0.01 / 0.32 | 0.01 / 0.32 |
| flight-path roll reached, first input | 107 / 37 °/s | 103 / 34 °/s |
| flight-path roll reached, reversal | 108 / 33 °/s | 102 / 32 °/s |
| roll the wrong way | 0 | 0 |
| departures | 0 | 0 |

- The roll rates are a little lower on average, probably because the jet is at a more negative AoA when the roll starts (it is no longer held at -18 to -30° first; not checked case by case). In both builds each roll ends no more than 8 °/s short of its own target, or above it.
- **Curves, pull → pushover (3 cases read every 0.5 s):** the AoA paths match 0.2.2's until the old limit used to bite, then continue straight to -40° (reaching -35° 0.07–0.28 s sooner). The first 0.5 s after the stick goes forward is slow in both builds (AoA 34 → 32.5°): the pitch-rate target ramps from +19 °/s at the 72–76 deg/s² of nose-down the jet has there. Smooth, no hitch.
- With the one-axis demand on both sides instead (`CplLateral=false`): limit near -18° always, sideslip worst 8.0°, 0.97 s above 5°.

**Unchanged:** the full suite (regression set, stop / reversal sweep, corkscrews, damage set) and the 88-run loaded-reversal set give the same tables as 0.2.2, line for line.

## Cost
More solves per sweep while the AoA is positive. Bench, the sweep per tick, median of 3 runs with nothing else running: 432 → 470 µs on the 11:33 window (+9 %), 368 → 380 µs on the 09:38 window (+3 %). 0.3.0 moves the solver to the native core.

## Install
- Main DLL replaced, 0.2.2 kept as `Ternion_FS3_FCS.dll.0.2.2.bak`. Dev DLL unchanged.
- **Mod manager:** update the version there, or its modpack may put the old DLL back.
- **To check in flight:** same load, pull to the limit, then push straight over. The push should run to the negative limit in one go, with no soft stop near -20° that then moves.
