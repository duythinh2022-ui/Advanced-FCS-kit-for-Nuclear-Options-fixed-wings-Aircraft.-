# FS-12 Revoker FCS 1.0.1: the F-22E loaded roll-reversal fix (4.1.1 + 4.1.2)

Base: 1.0.0. One control-law change, in the roll trajectory's reversal branch, ported line for line from F-22E 4.1.2 (`F22E_loaded_reversal_4.1.0_to_4.1.2.diff`). It acts only with an asymmetric load over 4 cm of lateral CG offset.

**Changelog:** With an asymmetric load, a roll reversal toward the heavy side no longer brakes gently with the roll surfaces near neutral and then kicks into the new roll: the roll acceleration builds once from the stick flip, paced by the rudders' travel, and keeps one value through the zero crossing. Against the load, the new roll starts at the acceleration its brake had instead of jumping to the unlimited figure.

**Status:** compiled against the game's assemblies, benched, **not flown**.

## The change
- Same six edits as F-22E 4.1.0 → 4.1.2, same constants; nothing in `Plugin.cs`, no setting, no telemetry column, no stored state added. Bench switches `RevOneAccel` / `RevLoadRamp` (false / false = 1.0.0, byte-identical on all 88 loaded runs).
- The yaw axis's crossing time is measured from the rudders' positions and servo rate. This jet: one rudder, ±25°, 100°/s: 0.25 – 0.5 s to cross.

## Bench: loaded reversals (F-22E 4.1.2's set, on this jet)
Store on `wing1b_L`, 560 / 850 / 1,160 / 1,570 kg for 0.08 / 0.12 / 0.16 / 0.21 m of lateral CG offset (the F-22E's 700 / 1,100 / 1,500 / 2,000 kg). 34 single reversals with full or part aft stick, both directions, plus 54 roll-spam runs. `_R` = reversing toward the heavy side.

**Curve check:** "re-rise" is the largest climb in roll acceleration after it first stops climbing, from the stick flip to 95 % of the new rate, as a share of that first peak. One build that then holds or eases off ≈ 0. "Stop, then spike" = the size of the spike.

| | toward the load (17 cases, AoA 9 – 58°, 182 – 384 kt) | against the load (17 cases) |
|---|---|---|
| re-rise, mean share of the first peak | 0.60 → **0.13** | 0.47 → **0.19** |
| cases with a re-rise over 25 % | 17 → **5** | 15 → **4** |
| re-rise, mean / worst (deg/s²) | 100 / 226 → 20 / 58 | 76 / 152 → 28 / 95 |
| sideslip max, range | 0.2 – 5.2° → 0.5 – 5.1° (per case -0.4 … +1.3, mean +0.10) | 1.2 – 5.2° → 1.0 – 5.1° (mean -0.11) |
| stick flip → roll stopped, mean change | -0.05 s | +0.00 s |

**Roll spam, 54 runs, full aft held** (mean, worst):
| | 1.0.0 | 1.0.1 |
|---|---|---|
| largest acceleration step at a zero crossing (deg/s² in 3 ticks) | 78 | **17** |
| sideslip max | 4.04° (5.3) | 4.02° (5.3) |
| seconds above 5° sideslip | 0.03 | 0.04 |
| rate-target retreats with the stick held (total) | 3 | 1 |
| bank angle travelled | 434° | 444° |

- **Leftover:** 5 of the 17 toward-the-load cases still re-rise 25 – 36 % (worst `s2_1100_580_R`, 0.40 → 0.36). They are the 0.08 – 0.12 m loads at 480 – 580 km/h, where the load gate is only half to fully on, the rudders cross in 0.25 s, and the acceleration still eases up a second time after the crossing.
- Pre-existing, not touched: at the zero crossing the canards' differential **command** jumps to +60 and back for a tick or two (same ticks in 1.0.0 and 1.0.1). Allocator behaviour, worth a look separately.

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
