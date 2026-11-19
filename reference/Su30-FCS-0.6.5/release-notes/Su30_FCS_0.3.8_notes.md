# Su-30VXN FCS 0.3.8: the F-22E loaded roll-reversal fix (4.1.1 + 4.1.2)

Base: 0.3.7 (your soft AoA-limit stick fix; 0.3.6 / 0.3.7 do not touch this part of the law, and the bench below is the same on 0.3.5 and 0.3.7). One control-law change, in the roll trajectory's reversal branch, ported line for line from F-22E 4.1.2 (`F22E_loaded_reversal_4.1.0_to_4.1.2.diff`). It acts only with an asymmetric load over 4 cm of lateral CG offset.

**Changelog:** With an asymmetric load, a roll reversal toward the heavy side no longer brakes gently with the roll surfaces near neutral and then kicks into the new roll: the roll acceleration builds once from the stick flip, paced by the rudders' travel, and keeps one value through the zero crossing. Against the load, the new roll starts at the acceleration its brake had instead of jumping to the unlimited figure.

**Status:** compiled against the game's assemblies, benched, **not flown**.

## The change
- Same six edits as F-22E 4.1.0 → 4.1.2, same constants; nothing in `Plugin.cs`, no setting, no telemetry column, no stored state added. Bench switches `RevOneAccel` / `RevLoadRamp` (false / false = 0.3.7, byte-identical on all 88 loaded runs).
- The yaw axis's crossing time is measured from the rudders' positions and servo rate. This jet: two rudders, ±35°, 100°/s: 0.35 – 0.7 s to cross (as the F-22E).

## Bench: loaded reversals (F-22E 4.1.2's set, on this jet)
Store on `Left_wing_middle`, 390 / 600 / 800 / 1,060 kg for 0.08 / 0.12 / 0.16 / 0.21 m of lateral CG offset (the F-22E's 700 / 1,100 / 1,500 / 2,000 kg). 34 single reversals with full or part aft stick, both directions, plus 54 roll-spam runs. `_R` = reversing toward the heavy side.

**Curve check:** "re-rise" is the largest climb in roll acceleration after it first stops climbing, from the stick flip to 95 % of the new rate, as a share of that first peak. One build that then holds or eases off ≈ 0. "Stop, then spike" = the size of the spike.

| | toward the load (10 cases, AoA 7 – 67°, 161 – 430 kt) | against the load (12 cases) |
|---|---|---|
| re-rise, mean share of the first peak | 0.78 → **0.05** | 0.64 → **0.19** |
| cases with a re-rise over 25 % | 8 → **0** | 10 → **3** |
| re-rise, mean / worst (deg/s²) | 113 / 228 → 17 / 80 | 107 / 271 → 37 / 205 |
| sideslip max, range | 2.5 – 11.3° → 2.7 – 11.9° (per case -1.8 … +2.8, mean +0.08) | 3.4 – 19.2° → 3.5 – 19.2° (mean +0.03) |
| stick flip → roll stopped, mean change | -0.13 s | +0.01 s |

**Roll spam, 54 runs, full aft held** (mean, worst):
| | 0.3.7 | 0.3.8 |
|---|---|---|
| largest acceleration step at a zero crossing (deg/s² in 3 ticks) | 55 | **8** |
| sideslip max | 10.01° (31.7) | 10.15° (28.6) |
| seconds above 5° sideslip | 2.06 | 2.09 |
| rate-target retreats with the stick held (total) | 60 | 61 |
| bank angle travelled | 275° | 278° |

- **Cost:** toward the load the brake starts harder (e.g. 104 → 152, 116 → 170 deg/s²), and sideslip goes either way: −1.8 … +2.8° per case. Worst `s2_700_580_R` (0.08 m, 330 kt, AoA 23°): 9.1 → 11.9°. Second worst `s4_1500_600_R`: 4.1 → 5.9°.
- Many of the slow cases (420 – 480 km/h full aft) go past 55 – 75° AoA before the reversal and the roll there barely reverses within the run (no reversal found, both builds). The table covers 161 – 430 kt, AoA 7 – 67°.
- The "roll behind its command" column reads high above ~50° AoA in both builds: it compares the trajectory's velocity-vector roll with the body roll rate, which at 60° is about half of it. Not a regression.

## Identity
- Suite (regression set, stop / reversal sweep, corkscrews, damage set): every per-case file identical to 0.3.7 except the wall-clock timing columns; the tables are identical too.
- `RevOneAccel=false RevLoadRamp=false`: 88 / 88 loaded runs byte-identical to 0.3.7.
- Plugin built twice: same bytes.

## Bench additions (in the source zip)
- `ifly` takes `storeKg=` / `storePart=` (an asymmetric store, as the single-case mode had), `dbg=… dbgcsv=<path>` (FCS fields per tick to a CSV) and `surfdump=1` (each surface's kind, travel, rate).
- `bench/an/loadrev.sh <tag>`: the loaded-reversal set above; `loadrev_cmp.py` run by run; `lrcurve.py` one case's curve every 3 ticks.

## Install
- Main DLL replaced, 0.3.7 kept as `.dll.0.3.7.bak`. Dev DLL unchanged (it has no code; the 0.3.7 file).
- **Mod manager:** update the version there, or its modpack may put the old DLL back.
- **To check in flight:** asymmetric load, full aft, roll away from the heavy side, reverse. The ailerons should sweep straight through to the new side instead of resting near neutral and then jumping.
