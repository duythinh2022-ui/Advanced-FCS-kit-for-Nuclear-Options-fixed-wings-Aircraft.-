# FS-12 Revoker FCS 1.5.0 — shared asymmetric-load code (the Compass / Su-30 fixes this jet lacked)

Base: 1.4.2. Minor bump: the asymmetric-load code is now the shared file all seven FCS mods use, so this jet gains the fixes the
Compass line and the Su-30 had and it did not. **Status:** compiled against the game's assemblies, benched. **Not flown.**

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
- **Pitch-cap figure from the coupled check with the nozzles working too** (see above; 1.4.2 used it only without the nozzles). It
  raises the limit wherever the nozzles work (not separated on the bench from the other changes in the loaded-reversal figures below).
- **Damage gate** (`AsymDamageGate`, Compass 0.1.8): the asymmetric-load protections (coupling roll limit, pitch priority, stab anti-cost,
  the coupled check) also engage on a damage asymmetry, not only on a lateral CG offset (`AsymGate()` in `LateralLaw` and `Tick`).
- **Pitch bias** (`AsymPitchBias`, Compass 0.1.7) and the no-TVC pitch-cap floors 7 / 4 deg: without pitch TVC, the nose-up the cap's
  model leaves out is learned and taken off the nose-down figure (pitch cap, coupling roll limit, capture braking).
- **Capture brake under the cap** (`AsymCapBrake`, Compass 0.1.5): without pitch TVC and with the cap in force, the AoA capture plans its
  stop with half the modelled nose-down at the cap.
- **Gap tighten** (`AsymCapGapTighten`, Compass 0.1.9): a limit that has to come down a long way comes down up to 4x faster.
- **Thrust about the real CG** (`AsymThrustCgLoad`), the lateral coupled demand (`CplLateral`), the low-thrust authority cap from the
  predicted dynamic pressure (`AuthCapPredict`, off), the no-gain pitch skip.
- Kept as this jet had them: the stab-travel reserve in the LP (`CplStabTravelLp` on, `AsymStabTravel` 40), the yaw nozzle as its own
  LP effector, `CplStabPeakRule` / `CplTopEdge` off, the canard attached-flow window (now on the allocator's own condition: at
  positive AoA only without the nozzles, as `CanWinNegOnly` has it), `AsymCoupledReserveMax` 0.7 / `NoTvc` 0.8. The canards are now
  marked as canards (`isCanard`, from their names), as on the Ternion and Su-30.
- Log: 7 new columns after `pitchMarg`: `asymPitchBias, asymPitchBiasNeg, asymCapPitchNeg, dmgGateHold, cplStopP, cplStopN, cplRef`.

## Bench (1.4.2 vs 1.5.0)
- Asymmetric-load matrix (25 cases, 560 / 1160 / 2000 kg on `wing1b_L`, TVC on and locked): 18 cases within 1.5 deg of 1.4.2 on
  every figure. The others:
  - 1160 kg, 300 km/h full aft: AoA past the limit 3.5 -> 6.8 deg at 47 m/s (peak 56.7 -> 59.6, sideslip 1.3 -> 1.7). Cause:
    gap tighten brings the limit down faster than the nose follows at that speed; with it off the case matches 1.4.2.
  - 1160 kg, 450 km/h: limit 44.4 -> 46.0 deg at the top. 560 kg cases: lowest limit 2-3 deg higher.
  - Roll reversal 300 km/h to the left: limit 28.3 -> 30.6 deg. Throttle step 350 km/h: peak AoA 32.4 -> 28.2 (limit 33.0 -> 27.9).
- Loaded-reversal set (54 roll-spam runs, better / same / worse): sideslip max 11 / 27 / 16 (mean 1.90 -> 2.00 deg, worst 3.55 -> 3.65),
  AoA above the cap 24 / 22 / 8, roll behind command (max) 5 / 35 / 14, bank travelled 7 / 30 / 17, steps at the zero crossing
  8 / 43 / 3. Mean peak AoA 45.3 -> 46.4 deg: the limit sits about 1 deg higher and gives a little roll for it.
- Clean jet: regression 21 / 21, stop / reversal sweep 33 / 33, corkscrews 21 / 21 identical. Damage suite table identical line for line.

## Install
- `BepInEx\plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` 1.5.0; 1.4.2 kept as `Revoker_FS12_FCS.dll.1.4.2.bak`. `meta.json` updated.
- Dev DLL unchanged. Release folder: `C:\Games\NO mods\Revoker-FCS-1.5.0-release`.
