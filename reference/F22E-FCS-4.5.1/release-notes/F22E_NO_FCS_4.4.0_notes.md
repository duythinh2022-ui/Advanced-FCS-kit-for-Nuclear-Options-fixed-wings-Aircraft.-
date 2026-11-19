# F-22E Strike Raptor FCS 4.4.0 — shared asymmetric-load code (the Compass / Su-30 fixes this jet lacked)

Base: 4.3.0. Minor bump: the asymmetric-load code is now the shared file all seven FCS mods use. **Status:** compiled against the
game's assemblies, benched. **Not flown.** As shipped (coupled check off) it flies as 4.3.0 on every bench set but one roll case.

## One asymmetric-load code for all seven FCS mods

Tom, 17 Nov 10:08: "make all of them use the same thing"; 10:26: option 3 (the same code everywhere, from the Su-30; the Su-30's
remaining-useful-authority limit on the Su-30 only; tuned numbers that differ for no airframe reason stay per jet).

**How it is built now**
- `AsymShared.cs` (new), `AsymCap.cs`, `CoupledCap.cs`: the same text in all seven mods; only the `namespace` line differs. `FcsController`
  is a `partial` class, so the shared file holds the asymmetric-load methods and their state; each jet's `FcsController.cs` keeps
  its own laws. Check: `tools/asym_shared/checkshared.py` (sha256 of the namespace-normalised files: AsymShared `62f3250027774425`,
  AsymCap `f1b8d2915c6e087c`, CoupledCap `6f66ef4087ade420`, identical in Vortex, Revoker, Ifrit, Compass, Ternion, Su-30, F-22E).
- What is in it: the sweep (`AsymSweepPoints` / `AsymSweepPoint`, the coupled LP check `CplSolve`, `SwGather` / `SwGridGeom` /
  `HingeSnaps`), the cap (`AsymStep`, `CplKnee`, `CplDemand`, `PushCapSample`, `CapFilterStep`, gap tighten), the low-thrust authority cap
  (`AuthCapStep` / `AuthCapFromSweep`), `FuelTrimStep`, `DamageGateStep`, `AsymGate`, and the state they own.
- Base: Su-30 0.6.2, with the pieces the other jets had that it lacked, each acting only where that jet has the hardware:
  - Revoker line: hinged-flap snapshot (the sweep stays parallel with hinged flaps active), the yaw nozzle as its own LP effector,
    the stab-travel reserve (`AsymStabTravel`) and the canard attached-flow window in the LP (now on the allocator's own condition).
  - Ifrit: the differential nozzles as an LP effector, free nozzles at low thrust (their range from neutral, not the whole travel,
    for the pitch cap: the neutral thrust moment is already in the LP's load), the sliding engine-out limit hook (`eoCapDyn`),
    no pitch cap when the margin is short already at the lowest AoA (`AsymPitchNoGainSkip`), the pitch-grid nozzles out of the LP
    while the law has them out (`noTvcOn`).
  - Ternion: the slat schedule on the LP's load (per-jet hooks), the engine-out zero-thrust match (no engine-out AoA cap where the
    jet has the match, `Plugin.EngineOutZeroThrust`), the design-sideslip roll telemetry.
- The pitch cap's held-pitch figure comes from the coupled check whenever the check runs (Su-30 / Compass line). Revoker, Ifrit,
  F-22E and Vortex used it only without the nozzles (`cplPitchGate`); with the nozzles working they took the held-load sweep's figure
  with the nozzles' whole travel and a fixed 20 deg/s^2 margin. On those jets, with the nozzles working, this is the larger change: the
  coupled check finds more nose-down than that figure, the pitch cap moves up and the roll-reserve limit is the one that holds.
- Per-jet settings (each jet's own files):
  - `FcsController.cs`: `AsymJetName`; `CplRefAuth`, `CplKneeOn0` (option 3: Su-30 only); `CplStabPeakRule`, `CplTopEdge` (Ifrit / Su-30 /
    F-22E on; Revoker / Vortex / Compass / Ternion off, see below); `CplStabTravelLp` (Revoker, Vortex); `AsymStabTravel`;
    `SwSlatSchedOn` / `SwHlAreas` / `SwSlatPos` (Ternion's slats; empty elsewhere).
  - `Plugin.cs`: `AsymStopTime` (Su-30 0.8, others 0), `AsymCoupledCheck` (F-22E off as shipped in 4.2.0), `CanardSymmetric`,
    `EngineOutZeroThrust` (Ternion), the reserve shares `AsymCoupledReserveMax` / `NoTvc`, and the tuned numbers that already were per jet.
  - Effectors: `TvcEffector.Mn / MnOn / MnNow / MnAllAxes / SharedTablesOn` (the Su-30's canted per-engine tables, one pitch grid on the
    others, none on the no-TVC jets), `SurfaceEffector.isCanard` (named canards).
- Two Ifrit / Su-30 rules stay per jet (`CplStabPeakRule`, `CplTopEdge`): on the Compass bench (900 kg on `wing1_L`) the top-edge cap
  search let the 300 km/h roll reversal depart (60 deg AoA, 40 deg sideslip; off: 24 deg, 2 deg), and the stab-peak rule put the AoA 3
  deg further past the limit after a throttle chop. Each jet keeps the state it had.

## What is new on this jet
- With the coupled check off (the 4.2.0 default, unchanged): the **damage gate**, **pitch bias** with the no-TVC pitch-cap floors
  7 / 4 deg, and the **capture brake under the cap** act only without pitch TVC (nozzles locked or thrust too low for them). **Thrust
  about the real CG** (`AsymThrustCgLoad`) and the **sliding engine-out limit** hook (`eoCapDyn`) are new here too.
- With `AsymCoupledCheck` on (dev config): the pitch-cap figure comes from the coupled check with the nozzles working too (1.3.0-line
  jets used it only without them), the lateral coupled demand (`CplLateral`), free nozzles at their range from neutral.
- To run the shared code this jet gained, without changing what it does: `SurfaceEffector.GridGeom(ref ctx, defl)` and its fresh-for
  check (ported from the Vortex), `KneeAoA` (returns the stall AoA: no settled-slope knee on this jet), the `tvcd` / `tvcy` fields, an
  empty hinged-flap list (`HingeFlapEffector` stub), `SettledSlopeFrac` 0.15, `CanardAttachedWindow` off (no canards).
- Kept: `CplStabPeakRule` / `CplTopEdge` on, no stab-travel reserve (`AsymStabTravel` 0), the gap tighten (this jet had it since the
  5 Nov stab-shot-off log), `AsymCoupledReserveMax` 0.7 / `NoTvc` 0.8.
- Log: 7 new columns after `pitchMarg`: `asymPitchBias, asymPitchBiasNeg, asymCapPitchNeg, dmgGateHold, cplStopP, cplStopN, cplRef`.

## Bench (4.3.0 vs 4.4.0)
- Asymmetric-load matrix as shipped (24 cases; 3000 / 2000 / 1000 kg on `WingRear_L`; TVC on and locked): **23 / 24 identical**. The
  one that differs is the nozzles-locked roll at 400 km/h (216 kt): peak roll rate 119.1 -> 117.3 deg/s, bank drift max 101.8 -> 100.3
  deg, IAS top 581.7 -> 581.8 km/h; AoA, limit and sideslip identical.
- Same matrix with `AsymCoupledCheck=true` on both builds: 10 / 24 identical, 19 within 1 deg. The others:
  - 1000 kg, 450 km/h full aft: lowest AoA 43.3 -> 39.7 deg, lowest limit 38.5 -> 33.5 deg, AoA past the limit 16.7 -> 19.4 deg,
    bank drift max 7.3 -> 5.2 deg. At 300 km/h: bank drift max 8.2 -> 3.5 deg, raw-limit jumps 5 -> 7. Both builds hunt on this case
    (5-7 jumps, AoA 17-19 deg past the limit at the low point): the 1000 kg limit with the check on is an open item on 4.3.0 as well.
  - Nozzles-locked roll 400 km/h: peak roll rate 99.8 -> 96.8 deg/s, bank drift max 94.2 -> 91.9 deg.
  - Roll reversals at 300 km/h: within 1.1 deg / 0.1 deg/s.
- Loaded-reversal set 88 / 88 identical. Regression 19 / 19, corkscrews 21 / 21, damage-suite table: identical.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 4.4.0; 4.3.0 kept as
  `Aryx_F22E_StrikeRaptor_FCS.dll.4.3.0.bak`. `meta.json` (manifest) updated with the new zip's hash.
- Dev DLL unchanged. Release folder: `C:\Games\NO mods\F22E-FCS-4.4.0-release`. The bench airframe data stays out of the source zip
  (Aryx's data).
