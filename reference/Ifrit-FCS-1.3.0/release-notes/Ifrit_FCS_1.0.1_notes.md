# KR-67 Ifrit FCS 1.0.1: the F-22E loaded roll-reversal fix (4.1.1 + 4.1.2)

Base: 1.0.0. One control-law change, in the roll trajectory's reversal branch, ported line for line from F-22E 4.1.2 (`F22E_loaded_reversal_4.1.0_to_4.1.2.diff`). It acts only with an asymmetric load over 4 cm of lateral CG offset.

**Changelog:** With an asymmetric load, a roll reversal toward the heavy side no longer brakes gently with the roll surfaces near neutral and then kicks into the new roll: the roll acceleration builds once from the stick flip, paced by the rudders' travel, and keeps one value through the zero crossing. Against the load, the new roll starts at the acceleration its brake had instead of jumping to the unlimited figure.

**Status:** compiled against the game's assemblies, benched, **not flown**.

## The change
- Same six edits as F-22E 4.1.0 → 4.1.2, same constants; nothing in `Plugin.cs`, no setting, no telemetry column, no stored state added. Bench switches `RevOneAccel` / `RevLoadRamp` (false / false = 1.0.0, byte-identical on all 88 loaded runs).
- The yaw axis's crossing time is measured from the rudders' positions and servo rate. This jet: **none**: the Ifrit has no surface the FCS classes as a rudder (its tail surfaces are named elevator and classed as stabilators), so the rudder-paced build-up (the 4.1.2 half) never switches on; only the 4.1.1 half acts.

## Bench: loaded reversals (F-22E 4.1.2's set, on this jet)
Store on `wing1_L`, 630 / 950 / 1,280 / 1,710 kg for 0.08 / 0.12 / 0.16 / 0.21 m of lateral CG offset (the F-22E's 700 / 1,100 / 1,500 / 2,000 kg). 34 single reversals with full or part aft stick, both directions, plus 54 roll-spam runs. `_R` = reversing toward the heavy side.

**Curve check:** "re-rise" is the largest climb in roll acceleration after it first stops climbing, from the stick flip to 95 % of the new rate, as a share of that first peak. One build that then holds or eases off ≈ 0. "Stop, then spike" = the size of the spike.

| | toward the load (8 cases, AoA 6 – 64°, 111 – 389 kt) | against the load (7 cases) |
|---|---|---|
| re-rise, mean share of the first peak | 1.37 → **0.92** | 0.70 → **0.45** |
| cases with a re-rise over 25 % | 6 → **5** | 5 → **4** |
| re-rise, mean / worst (deg/s²) | 97 / 164 → 60 / 127 | 78 / 170 → 41 / 93 |
| sideslip max, range | 2.9 – 5.5° → 2.9 – 5.4° (per case -0.4 … +0.0, mean -0.09) | 2.1 – 5.2° → 2.1 – 4.8° (mean -0.10) |
| stick flip → roll stopped, mean change | +0.00 s | +0.00 s |

**Roll spam, 54 runs, full aft held** (mean, worst):
| | 1.0.0 | 1.0.1 |
|---|---|---|
| largest acceleration step at a zero crossing (deg/s² in 3 ticks) | 20 | **6** |
| sideslip max | 5.04° (8.4) | 5.00° (8.4) |
| seconds above 5° sideslip | 0.52 | 0.48 |
| rate-target retreats with the stick held (total) | 59 | 59 |
| bank angle travelled | 174° | 166° |

- **What it gives on the Ifrit:** the jump at the zero crossing becomes continuous and later (largest 3-tick step 56 – 59 → 13 – 21 deg/s²; the peak 0.12 – 0.15 s → 0.32 – 0.38 s after the crossing, 2.2 – 3.3 → 1.7 – 2.7 × the brake). That's the F-22E 4.1.1 result you flew on 10 Nov ("still not fully fixed, it is just smoother").
- **Cost:** roll spam bank angle travelled 174 → 166° mean (24 of 54 runs lower).
- **To get the 4.1.2 half on the Ifrit** the build-up needs a measure of how long the Ifrit's yaw effectors take to swing across. Its ruddervators carry pitch too and its roll TVC yaws, so the rudders' travel time doesn't translate directly. Your call whether to do that next.

## Identity
- Suite (regression set, stop / reversal sweep, corkscrews, damage set): every per-case file identical to 1.0.0 except the wall-clock timing columns; the tables are identical too.
- `RevOneAccel=false RevLoadRamp=false`: 88 / 88 loaded runs byte-identical to 1.0.0.
- Plugin built twice: same bytes.

## Bench additions (in the source zip)
- `ifly` takes `storeKg=` / `storePart=` (an asymmetric store, as the single-case mode had), `dbg=… dbgcsv=<path>` (FCS fields per tick to a CSV) and `surfdump=1` (each surface's kind, travel, rate).
- `bench/an/loadrev.sh <tag>`: the loaded-reversal set above; `loadrev_cmp.py` run by run; `lrcurve.py` one case's curve every 3 ticks.

## Install
- Main DLL replaced, 1.0.0 kept as `.dll.1.0.0.bak`. Dev DLL unchanged (it has no code; the 1.0.0 file).
- **Mod manager:** update the version there, or its modpack may put the old DLL back.
- **To check in flight:** asymmetric load, full aft, roll away from the heavy side, reverse. The ailerons should sweep straight through to the new side instead of resting near neutral and then jumping.
