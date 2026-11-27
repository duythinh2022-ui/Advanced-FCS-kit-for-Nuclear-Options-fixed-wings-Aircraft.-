# KR-67 Ifrit FCS 1.4.0 — shared asymmetric-load code (the Compass / Su-30 fixes this jet lacked)

Base: 1.3.0. Minor bump: the asymmetric-load code is now the shared file all seven FCS mods use, so this jet gains the fixes the
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
- **Gap tighten** (`AsymCapGapTighten`, Compass 0.1.9): a limit that has to come down a long way (a part shot off) comes down up to 4x
  faster than the 10 deg/s tighten rate. All the damage-suite differences below come from this alone (with it off the suite matches 1.3.0
  line for line).
- **Pitch-cap figure from the coupled check with the nozzles working too** (Su-30 / Compass line, see above). 1.3.0 used it only without
  the nozzles (`cplPitchGate`).
- **Damage gate** (`AsymDamageGate`), **pitch bias** (`AsymPitchBias`) and the no-TVC pitch-cap floors 7 / 4 deg, **capture brake
  under the cap** (`AsymCapBrake`, half the modelled nose-down at the cap): these act only without pitch TVC (nozzles locked, or
  thrust too low for them).
- **Thrust about the real CG** (`AsymThrustCgLoad`), the lateral coupled demand (`CplLateral`), the low-thrust authority cap from the
  predicted dynamic pressure (`AuthCapPredict`, off).
- Kept as this jet had them (most of them came from this jet): the differential nozzles as an LP effector, free nozzles at low thrust
  (now their range from neutral, not the whole travel, in the pitch cap), the sliding engine-out limit (`eoCapDyn`), the no-gain pitch
  skip, the pitch-grid nozzles out of the LP while the law has them out (`noTvcOn`), `CplStabPeakRule` / `CplTopEdge` on, no stab-travel
  reserve (`AsymStabTravel` 0), `AsymCoupledCheck` on, `AsymCoupledReserveMax` 0.75 / `NoTvc` 0.8.
- Log: 7 new columns after `pitchMarg`: `asymPitchBias, asymPitchBiasNeg, asymCapPitchNeg, dmgGateHold, cplStopP, cplStopN, cplRef`.

## Bench (1.3.0 vs 1.4.0)
- Asymmetric-load matrix (25 cases; 2450 / 1280 / 630 kg on `wing1_L`, about 30 / 16 / 8 cm of lateral CG offset; TVC on and
  locked): 16 cases within 1.5 deg of 1.3.0 on every figure. The others:
  - Throttle step at 350 km/h (189 kt), afterburner to idle on the limit: peak AoA 45.5 -> 44.1 deg, limit top 48.2 -> 46.6 deg; AoA
    past the live limit 3.9 -> 8.9 deg. Cause: gap tighten drops the limit faster than the nose follows after the chop; the AoA itself
    is lower. Sideslip 3.8 -> 3.7 deg, bank drift 21.9 -> 23.7 deg max.
  - Nozzles locked, 300 / 450 km/h full aft: the capture brake holds the nose lower on the way in. Lowest AoA 19.9 -> 19.2 and
    20.0 -> 18.6 deg; limit unchanged (20.2-21.0 deg). Nozzles-locked roll at 400 km/h: lowest AoA 19.8 -> 18.8 deg, sideslip 1.6 -> 2.0
    deg, peak roll rate 34.0 -> 31.9 deg/s.
  - Pushes, 300 / 450 km/h: raw-limit jumps (more than 10 deg inside 0.5 s) 2 -> 0; raw limit lowest 12.6 -> 22.4 and 11.7 -> 23.0 deg.
    The AoA itself is unchanged.
  - Half-throttle pull, 450 km/h: AoA past the limit 1.0 -> 0.3 deg; sideslip 2.0 -> 2.7 deg.
  - Throttle step at 250 km/h (135 kt), idle to afterburner: raw-limit jump 1 -> 0, peak AoA 22.4 -> 23.6 deg against a limit at 22.6.
  - Roll reversal 450 km/h to the left: bank drift max 71.3 -> 74.4 deg, the rest within 0.6.
- Loaded-reversal set (54 roll-spam runs, better / same / worse): sideslip max 23 / 12 / 19 (mean 5.29 -> 5.21 deg, worst 8.52 -> 8.40),
  seconds above 5 deg sideslip 20 / 22 / 12 (mean 0.46 -> 0.36 s, worst 2.57 -> 1.43 s), roll behind command (rms) 26 / 10 / 18,
  bank travelled 23 / 6 / 25 (mean 244 -> 238 deg), AoA above the cap 16 / 22 / 16 (mean 1.52 -> 1.46, worst 3.71 -> 3.76 deg),
  steps at the zero crossing 12 / 31 / 11 (worst 31.4 -> 38.3 deg/s^2 in 3 ticks). Single reversals: `s2_1100_480_L` has no usable
  reversal on either build (1.3.0: 0 deg/s built 0.1 s after the stick; 1.4.0: the script finds no reversal).
- Clean jet: regression 21 / 21, stop / reversal sweep 33 / 33, corkscrews 21 / 21 identical.
- Damage suite (52 cases): 18 cases differ, all with one or both ruddervators lost, all from gap tighten. The limit after the loss
  comes down at 40 deg/s instead of 10. One ruddervator, leaf at 370 km/h: sideslip peak 7.6 -> 9.1 deg (stop) and 7.9 -> 10.6 deg
  (reversal), lowest AoA after the event 11.9 -> 11.7 and 11.4 -> 9.6 deg. At 296 km/h: sideslip 6.5 -> 6.6 deg. Pushover held at
  560 km/h: sideslip 13.9 -> 14.8 deg; at 600 km/h released: 4.3 -> 5.8 deg. The faster pitch-down costs 1-3 deg of extra sideslip
  on the way down on this jet.

## Install
- `BepInEx\plugins\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` 1.4.0; 1.3.0 kept as `Ifrit_KR67_FCS.dll.1.3.0.bak`. `meta.json` updated.
- Dev DLL unchanged. Release folder: `C:\Games\NO mods\Ifrit-FCS-1.4.0-release`.
