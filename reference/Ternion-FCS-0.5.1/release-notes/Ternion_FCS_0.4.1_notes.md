# FS-3 Ternion FCS 0.4.1 — shared asymmetric-load code

Base: 0.4.0. Patch bump: the asymmetric-load code is now the shared file all seven FCS mods use. Same laws; the sweep is scheduled
slightly differently (below). **Status:** compiled, benched. **Not flown**.

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

## This jet
- Settings: slat schedule on the LP's load (your `SweepSlatSchedule` / `SlatAoASchedule`, through the per-jet hooks), engine-out
  zero-thrust match on (`EngineOutZeroThrust`), `CplStabPeakRule` / `CplTopEdge` off (as 0.4.0), canards differential (`CanardSymmetric` off).
- One scheduling change: 0.4.0 set the coupled check's gates inside each chunk of sweep points, so the first chunk of a new sweep was
  sized with the previous sweep's state. The shared code latches them when the sweep starts, so each point is now evaluated one physics
  tick apart from 0.4.0. That moves the sweep's figures by about 1 part in 800 (roll authority 862 -> 863 deg/s^2, reserves +-1.1).
- The coupled check's fallback when it has no effectors (every surface gone) is now the one-axis demand (as the Compass and Su-30),
  not 0.

## Bench (0.4.0 vs 0.4.1)
- Asymmetric-load matrix (the Compass matrix ported to this jet: 770 / 1030 / 1370 kg on `wing_2a_L`, 24 cases; `bench/an/asymmat.sh`):
  21 cases within 1 deg / 1 deg/s of 0.4.0 on every figure; three differ in one figure each: peak roll rate 40.9 -> 43.8 deg/s
  (300 km/h reversal to the right), AoA past the limit on an idle pull 4.3 -> 2.1 deg, IAS 125 -> 128 km/h (1030 kg pull).
- Regression, stop / reversal sweep and corkscrew summary tables: identical line for line (the CSVs differ at the float level above).
- Damage suite: one case differs: "held max AoA 300 + engine R intake off" departs in both (peak AoA 85.6 -> 88.7 deg); the rest identical.

## Install
- `BepInEx\plugins\Ternion_FS3.FCS\Ternion_FS3_FCS.dll` 0.4.1; 0.4.0 kept as `Ternion_FS3_FCS.dll.0.4.0.bak`. `meta.json` updated.
- Dev DLL unchanged. Release folder: `C:\Games\NO mods\Ternion-FCS-0.4.1-release`.
