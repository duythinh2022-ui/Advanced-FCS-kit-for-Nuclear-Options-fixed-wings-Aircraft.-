# F-22E FCS 3.1.1: the timer splits "rest" (instrumentation only)

Base: 3.1.0. Patch bump. Compiled; benched byte-identical to 3.1.0; **not flown**. 3.1.0 kept as `.dll.3.1.0.bak`.

## Tom (5 Nov, 23:24)
"do 1": the first item of the zero-behaviour-change optimisation list, finding out where the in-game ~740 µs "rest" goes.

## What's new
**Timestamp pairs around:**
- `PitchLaw` + `LateralLaw`
- `AsymStep`, and inside it:
  - `AsymMomentAt`
  - `SlipProbeStep_`
  - `AsymSweepPrep` + `AsymSweepPoints`
- `ComputeEnvelope` + `ComputeHybridSlopes` (every 0.1 s)
- `RebuildBodies` + `UpdateMassProps`
- `flaps.Update`
- `GroundSteer`
- `VmcApply`, timed by the hook

**The 10 s timing log gets a second line:** "rest split (us per tick)" with each of these, and what is still unaccounted.

**New CSV columns** after `tRowUs`: `tLawUs, tAsymUs, tAsymMomUs, tSlipUs, tSweepUs, tEnvUs, tMassUs, tFlapUs, tGndUs, tVmcUs`.

## Bench identity vs 3.1.0
- Suite: 153 / 153 per-case files identical.
- Replays: 52 / 52 identical.
- Corkscrew bench CSV: same md5.
- Game CSV: 0 / 2316 rows differ, with the timer columns set aside.

## What the bench already shows (telemetry off, µs per tick, 2-core VM)
| phase | µs |
|---|---|
| asym step | **180** |
| … of it the asym-load AoA sweep (`AsymSweepPoints`) | **161** |
| … asym moment / slip probe | 10 / 6 |
| table rebuild | 101 |
| allocator | 67 |
| envelope | 23 |
| laws | 18 |
| flaps / mass / ground / VmcApply | 6 / 2 / 0.4 / 0.5 |

**The asymmetric-load sweep is the largest single item left.** The laws are cheap. The in-game split still has to come from a flight.

## Install
- **Main:** md5 `42a7238d1f1c1c1341016e14685ace8b`; 3.1.0 kept as `.dll.3.1.0.bak`.
- **Dev:** md5 `617fbe783bf08eafccf5e2985e3ca21a`; 3.1.0 kept as `.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.1.1-release`. The mod zip holds the main DLL only.
