# Su-30VXN Gandiva FCS 0.6.3 — shared asymmetric-load code (no change in how it flies)

Base: 0.6.2. Patch bump: the asymmetric-load code is now the shared file all seven FCS mods use. On this jet it flies exactly as 0.6.2.
**Status:** compiled against the game's assemblies, benched. **Not flown** (nothing to fly: the same laws).

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
- Settings: `CplRefAuth` and the `CplKnee` trade on (option 3), `AsymStopTime` 0.8, `CplStabPeakRule` / `CplTopEdge` on, `CanardSymmetric` (yours, on),
  `AsymCoupledReserveMaxNoTvc` = `AsymCoupledReserveMax` (0.8; the Su-30 always used one share).
- The canard attached-flow window stays out of this jet's LP, as before: the allocator only walks the canards into it when they are not
  symmetric, and on this jet they are; the shared check now uses the allocator's own condition.
- The stab-travel reserve in the LP (`CplStabTravelLp`) is off here, as before. With it on (the Revoker's rule) the slow full pulls on
  the matrix stopped opening the limit toward 80 deg (250 km/h pull + roll: peak AoA 79.9 -> 54.2 deg), so it stays a per-jet setting.
- The canted nozzles keep their own per-engine vectoring state in the LP at low thrust (`MnAllAxes`); the pitch-grid jets drop their
  nozzles from the LP while the law has them out (`noTvcOn`). On this jet that rule would have opened the idle-pull limit to 68 deg.

## Bench (0.6.2 vs 0.6.3, every logged column, timing columns excluded)
- Asymmetric-load matrix (31 cases, 800 / 1600 kg on `Left_wing_middle`, TVC on and locked): **31 / 31 identical**.
- Loaded-reversal set: **88 / 88 identical**. Regression set 21 / 21, stop / reversal sweep 33 / 33, corkscrews 21 / 21: identical;
  their summary tables identical line for line.
- The damage suite does not run on this jet's bench in either build (it points at the Ternion's damage file: every case errors the same way).

## Install
- `BepInEx\plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll` 0.6.3; 0.6.2 kept as `Su30_VXN_FCS.dll.0.6.2.bak`. `meta.json` updated (mod manager record).
- Dev DLL unchanged. Release folder: `C:\Games\NO mods\Su30-FCS-0.6.3-release`.
