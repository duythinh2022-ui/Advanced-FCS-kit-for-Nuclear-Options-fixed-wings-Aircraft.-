# FS-3 Ternion FCS 0.2.1: the F-22E loaded roll-reversal fix (4.1.1 + 4.1.2)

Base: 0.2.0. One control-law change, in the roll trajectory's reversal branch, ported line for line from F-22E 4.1.2 (`F22E_loaded_reversal_4.1.0_to_4.1.2.diff`). It acts only with an asymmetric load over 4 cm of lateral CG offset.

**Changelog:** With an asymmetric load, a roll reversal toward the heavy side no longer brakes gently with the roll surfaces near neutral and then kicks into the new roll: the roll acceleration builds once from the stick flip, paced by the rudders' travel, and keeps one value through the zero crossing. Against the load, the new roll starts at the acceleration its brake had instead of jumping to the unlimited figure.

**Status:** compiled against the game's assemblies, benched, **not flown**.

## The change
- Same six edits as F-22E 4.1.0 → 4.1.2, same constants; nothing in `Plugin.cs`, no setting, no telemetry column, no stored state added. Bench switches `RevOneAccel` / `RevLoadRamp` (false / false = 0.2.0, byte-identical on all 88 loaded runs).
- The yaw axis's crossing time is measured from the rudders' positions and servo rate. This jet: two rudders, ±35°, 100°/s: 0.35 – 0.7 s to cross (as the F-22E).

## Bench: loaded reversals (F-22E 4.1.2's set, on this jet)
Store on `wing_2a_L`, 510 / 770 / 1,030 / 1,370 kg for 0.08 / 0.12 / 0.16 / 0.21 m of lateral CG offset (the F-22E's 700 / 1,100 / 1,500 / 2,000 kg). 34 single reversals with full or part aft stick, both directions, plus 54 roll-spam runs. `_R` = reversing toward the heavy side.

**Curve check:** "re-rise" is the largest climb in roll acceleration after it first stops climbing, from the stick flip to 95 % of the new rate, as a share of that first peak. One build that then holds or eases off ≈ 0. "Stop, then spike" = the size of the spike.

| | toward the load (16 cases, AoA 4 – 50°, 171 – 447 kt) | against the load (17 cases) |
|---|---|---|
| re-rise, mean share of the first peak | 0.23 → **0.06** | 0.64 → **0.47** |
| cases with a re-rise over 25 % | 8 → **1** | 9 → **5** |
| re-rise, mean / worst (deg/s²) | 28 / 82 → 9 / 31 | 44 / 183 → 26 / 186 |
| sideslip max, range | 0.7 – 4.1° → 0.7 – 4.1° (per case -0.2 … +0.2, mean +0.02) | 0.6 – 3.9° → 0.5 – 4.0° (mean +0.08) |
| stick flip → roll stopped, mean change | +0.01 s | +0.07 s |

**Roll spam, 54 runs, full aft held** (mean, worst):
| | 0.2.0 | 0.2.1 |
|---|---|---|
| largest acceleration step at a zero crossing (deg/s² in 3 ticks) | 81 | **47** |
| sideslip max | 2.48° (4.8) | 2.43° (4.9) |
| seconds above 5° sideslip | 0.00 | 0.00 |
| rate-target retreats with the stick held (total) | 7 | 5 |
| bank angle travelled | 260° | 265° |

- Several Ternion toward-the-load reversals start from a low roll rate (30 – 80 deg/s) and cross zero 0.25 – 0.4 s after the flip, before the 0.35 – 0.7 s build-up is done. The acceleration then keeps building through the crossing and comes down after it: one smooth build, no second rise. On a peak / brake ratio that reads as 1.3 – 2.1, which is why that column alone is misleading here.

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
