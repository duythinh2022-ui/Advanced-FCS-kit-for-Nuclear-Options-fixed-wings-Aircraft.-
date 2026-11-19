# Revoker FCS 0.2.22

Base: **0.2.21**. Knee-preserving elevon stall stagger, inboard pair reversing at ~28° (setting 26), outboard at 45°.

Compiled only. **Not benched, not flown.** The remap was checked by running the shipped C# (`FindPeakDeg`, `FindKneeDeg`, `MapKnee`, `BuildCharts`, extracted verbatim) on the game's `Fighter1_wing` curve and scoring the charts it produced. Takes effect on the next spawn.

## Why
0.2.20–0.2.21 compressed the whole lift curve to move the inboard peak. That dragged the foil's 17° knee down to 12–14°, costing the inboard pair roll authority at 10–15° (−6 to −30 % at 10°, ~−23 % at 15°) and some lift above the knee. It also left a second low point at ~18.5°, where both pairs sit on the same knee (0.08 even unstaggered) — the earlier 20–50° scoring missed it.

## Change
- **`MapKnee`:** below the source curve's knee the chart is untouched; the plateau knee→peak is mapped linearly onto knee→P; past P as before. Lift amplitude 1 everywhere: peak CL 1.10 kept.
- **`FindKneeDeg`:** first local minimum of the lift slope after its steepest point, if under 25 % of it (Fighter1_wing: 17.5° on the 0.25° scan, keyframe 17.19°); else where the slope first falls under 15 %; else 0 (whole-curve compression).
- **`InnerElevonReversalAoA` 37 → 26** (migration `RevokerConfigRevision` 14 → 15, only from exactly 37; a file still on 32 goes 32 → 37 → 26). `OuterElevonReversalAoA` stays 45.
- **`ElevonStaggerLiftSlope` retired** (peak CL always kept); the key is no longer bound.
- Log on spawn: `Revoker FCS staggered elevon stall on Fighter1: 4 parts get their own curves (inboard 26, outboard 45 deg, knee-preserving).`

## Checked on the shipped code
- Inboard chart identical to the game's curve for |AoA| ≤ 17.2° (max diff 8e-8); peak CL 1.099 / outboard 1.100 / base 1.100.
- Reversal (velocity-vector roll, elevon + camber on its wing section, measured arms): inboard **+27.8 / −27.5** small, **±33.3** at full ±25°; outboard **±44.9** small, **±40.6** at full ±30°.

**Available roll authority** (|inboard| + |outboard|; arms 3.04 / 3.18 m inboard elevon / wing2 host, 5.49 / 4.62 m outboard elevon / wingtip host):

| | floor small, 10–55° | floor full | 5° | 10° | 15° | 18.5° | 25° | 32° | 40° | 45° |
|---|---|---|---|---|---|---|---|---|---|---|
| 0.2.19 no stagger | 0.03 @44.5 | 0.01 @40.5 | 3.89 | 3.65 | 1.21 | 0.08 | 0.35 | 0.45 | 0.23 | 0.05 |
| 0.2.21 (37, whole-curve) | 0.15 @18 | 0.07 @40.5 | 3.92 | 3.45 | 0.89 | 0.15 | 0.38 | 0.39 | 0.23 | 0.16 |
| **0.2.22** | **0.24** @18.5 | **0.18** @40.5 | 3.89 | 3.65 | 1.21 | 0.24 | 0.50 | 0.39 | 0.38 | 0.29 |

Negative side symmetric (floor 0.24 at −18.5°).

## Side effects
- From ~28° to 45° the allocator flies the inboard pair reversed; that is what fills the band.
- Inboard lift: CL 1.09 at 25° (base 0.98), 1.07 at 35° (1.06), 0.96 at 45° (1.10) — about ±1 % of total wing lift, a small trim nudge each way for the canards.
- Below 17° nothing changes.

## Limits
Model assumes the allocator can use each pair independently, reversed included; canards and TVC not counted; the 18.5° floor is set by the outboard pair's own knee (only reshaping the outboard below its stall would raise it).

## Install
- **Main:** `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` (md5 63f1bfdcf45915541b227660ec82383d) and `meta.json`, version 0.2.22.
- **Backup:** 0.2.21 is `…\Revoker_FS12_FCS.dll.0.2.21.bak`.
- **Release:** `C:\Games\NO mods\Revoker-FCS-0.2.22-release`.
