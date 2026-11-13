# T/A-30 Compass FCS 0.2.1: the F-22E loaded roll-reversal fix (4.1.1 + 4.1.2)

Base: 0.2.0. One control-law change, in the roll trajectory's reversal branch, ported line for line from F-22E 4.1.2 (`F22E_loaded_reversal_4.1.0_to_4.1.2.diff`). It acts only with an asymmetric load over 4 cm of lateral CG offset.

**Changelog:** With an asymmetric load, a roll reversal toward the heavy side no longer brakes gently with the roll surfaces near neutral and then kicks into the new roll: the roll acceleration builds once from the stick flip, paced by the rudders' travel, and keeps one value through the zero crossing. Against the load, the new roll starts at the acceleration its brake had instead of jumping to the unlimited figure.

**Status:** compiled against the game's assemblies, benched, **not flown**.

## The change
- Same six edits as F-22E 4.1.0 → 4.1.2, same constants; nothing in `Plugin.cs`, no setting, no telemetry column, no stored state added. Bench switches `RevOneAccel` / `RevLoadRamp` (false / false = 0.2.0, byte-identical on all 88 loaded runs).
- The yaw axis's crossing time is measured from the rudders' positions and servo rate. This jet: two rudders, ±25°, 100°/s: 0.25 – 0.5 s to cross.

## Bench: loaded reversals (F-22E 4.1.2's set, on this jet)
Store on `wing1_L`, 210 / 320 / 430 / 570 kg for 0.08 / 0.12 / 0.16 / 0.21 m of lateral CG offset (the F-22E's 700 / 1,100 / 1,500 / 2,000 kg). 34 single reversals with full or part aft stick, both directions, plus 54 roll-spam runs. `_R` = reversing toward the heavy side.

**Curve check:** "re-rise" is the largest climb in roll acceleration after it first stops climbing, from the stick flip to 95 % of the new rate, as a share of that first peak. One build that then holds or eases off ≈ 0. "Stop, then spike" = the size of the spike.

| | toward the load (17 cases, AoA 4 – 30°, 110 – 359 kt) | against the load (17 cases) |
|---|---|---|
| re-rise, mean share of the first peak | 0.36 → **0.10** | 0.42 → **0.30** |
| cases with a re-rise over 25 % | 9 → **3** | 10 → **8** |
| re-rise, mean / worst (deg/s²) | 43 / 117 → 11 / 38 | 39 / 85 → 26 / 79 |
| sideslip max, range | 1.4 – 6.7° → 1.7 – 6.5° (per case -0.8 … +1.5, mean +0.29) | 1.0 – 5.1° → 0.8 – 5.6° (mean -0.07) |
| stick flip → roll stopped, mean change | -0.04 s | +0.01 s |

**Roll spam, 54 runs, full aft held** (mean, worst):
| | 0.2.0 | 0.2.1 |
|---|---|---|
| largest acceleration step at a zero crossing (deg/s² in 3 ticks) | 74 | **30** |
| sideslip max | 3.85° (6.9) | 4.04° (7.6) |
| seconds above 5° sideslip | 0.03 | 0.08 |
| rate-target retreats with the stick held (total) | 10 | 23 |
| bank angle travelled | 297° | 297° |

- **Leftover:** the Compass reverses so fast (zero crossing at 0.3 – 0.6 s) that the crossing often falls inside the one build-up. That's the intended shape (no second rise), but in 3 low-speed toward-the-load cases (420 – 600 km/h from a settled pull, 1,100 / 1,500 kg) the build-up pauses for ~0.1 s mid-brake and then climbs another 25 – 40 % (e.g. 84 → 110 deg/s²) before the crossing. Much smaller than 0.2.0's jump after the crossing (77 → 127), but not a single clean build.
- **Cost:** toward the load, sideslip up 0.1 – 1.5° in most cases (largest increase 1.4 → 2.9°; the highest case 6.5°, 0.2.0: 6.7°). Roll spam: rate-target retreats with the stick held 10 → 23 in total over 54 runs, sideslip max mean 3.85 → 4.04°.

## Identity
- Suite (regression set, stop / reversal sweep, corkscrews, damage set): every per-case file identical to 0.2.0 except the wall-clock timing columns; the tables are identical too.
- `RevOneAccel=false RevLoadRamp=false`: 88 / 88 loaded runs byte-identical to 0.2.0.
- Plugin built twice: same bytes.

## Bench additions (in the source zip)
- `ifly` takes `storeKg=` / `storePart=` (an asymmetric store, as the single-case mode had), `dbg=… dbgcsv=<path>` (FCS fields per tick to a CSV) and `surfdump=1` (each surface's kind, travel, rate).
- `bench/an/loadrev.sh <tag>`: the loaded-reversal set above; `loadrev_cmp.py` run by run; `lrcurve.py` one case's curve every 3 ticks.

## Install
- Main DLL replaced, 0.2.0 kept as `.dll.0.2.0.bak`. Dev DLL unchanged (it has no code; the 0.2.0 file).
- **Mod manager:** update the version there, or its modpack may put the old DLL back.
- **To check in flight:** asymmetric load, full aft, roll away from the heavy side, reverse. The ailerons should sweep straight through to the new side instead of resting near neutral and then jumping.
