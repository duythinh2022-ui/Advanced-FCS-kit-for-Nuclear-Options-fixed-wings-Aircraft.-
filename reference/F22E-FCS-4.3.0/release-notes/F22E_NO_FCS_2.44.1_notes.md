# F-22E FCS 2.44.1: cleanup (no behaviour change)

Base: 2.44.0. Compiled against the game's assemblies, bench-checked identical to 2.44.0, **not flown**. 2.44.0 kept as `.dll.2.44.0.bak`.
CSV unchanged.

## Tom (4 Nov, 21:46 / 21:51)
- "do cleanup for raptor"
- "also update dev dll"

## Removed: bench switches that were off in the shipped build, with their dead code
| switch | what it was | from |
|---|---|---|
| `PushGovInst` | the governor's one-tick two-axis rate cap (its `capInstG` term went with it) | 2.34 |
| `PushGovPace` | the governor's pace selector; the shipped choice, the smaller of the two paces, is now the code | 2.34 |
| `PushPlanOutA` | the plan bound blended to the stick linearly in AoA (rejected) | 2.35 |
| `PushPlanStabWin`, `PushPlanStabMargin`, `PushPlanStabOneSide` | stabs held in their attached window through a planned push, plus the one-sided window function `StallWindowDeep` | 2.36 |
| `PushPlanNoBand` | the governor's lateral band stood aside under a plan | 2.36 |
| `SweepWinUnderPlan` | was on; its "off" branch is gone (the sweep window acts under a plan, as before) | 2.36 |
| `PushPlanRelBudget`, `PushPlanRelFollow` | the released roll under a plan braked by the budget rule, or floored at the measured yaw's roll (both rejected in 2.41), plus the `pushPlanDRel` budget they read | 2.41 |
| `PushPlanReachAll` | the reach pass at all times (2.42.0's way, dropped) | 2.42 |
| `TwoHorizonPlan` | the two-horizon pick under a plan | 2.43 |
| `SweepWinWalk` | the sweep window walking a stab in instead of snapping | 2.42 |
| `LimitFollowsPath` | the alternative to `StickCancelsLimit` at the AoA limit | 2.44 |
| `Allocator.AttachStart` (+ `Effector.attLo/attHi`) | the allocator's multi-start from the attached range | 2.42 |
| `Allocator.ProbeNoReach` (+ `suppressReach`) | no reach pass in the capacity probes | 2.43 |

## Removed: the `ReleasedStickStabRateBound` setting (Dev file, "4. Gains")
- It has had no effect since 2.43.0, which replaced the bound with the reach model.
- 2.44.1 drops the line from `Aryx_F22E_StrikeRaptor.FCS.Dev.cfg` once, on first load. No value is carried over. The log reports
  "F-22E FCS: 1 retired device key(s) dropped from the dev file."
- How BepInEx handles this was checked against its own `ConfigFile`, with a test file that has the key plus a bound key. After the drop and
  save, only the bound key is left.

## Kept on purpose
- `StickCancelsLimit`, `ReachPass`, `TwoHorizon`, `TwoHorizonPath`, `PushPlanOwnsPitch`, `PushPlanClipKey`, `PushPlanWinOff`, `PushPlanHeld`.
  These switches are on, and they mark where each replacement acts.
- `FinLossAoACap` and the damage devices.
- The CSV `stabRateBound` column (always 0), so the log columns don't shift.
- Your tuning values: none touched.

## Dev DLL
- Rebuilt with its version set to 2.44.1 (it said 2.6.6). It has no code of its own: it only unlocks the Dev settings. The FCS finds it by
  its GUID, which is unchanged.
- The build was checked first: building the old 2.6.6 source reproduces the shipped 2.6.6 DLL byte for byte (same md5).

## Size
FcsController 9780 → 9692 lines, Allocator 1035 → 963. The game DLL is 3 KB smaller.

## Bench: identical to 2.44.0
- Hybrid replays: 61 released + 14 held pushes. Every per-tick output file is byte-identical.
- Free replays: 204 (pinned + free). Byte-identical.
- Suite: reversals, corkscrews, regression set, utilisation, damage. Identical apart from the run's tag in the first line.

## Not verified
- Not flown. Nothing the bench runs has changed. The one piece the bench cannot run is the Dev-file key drop at game start, which was
  checked against BepInEx separately (above).
