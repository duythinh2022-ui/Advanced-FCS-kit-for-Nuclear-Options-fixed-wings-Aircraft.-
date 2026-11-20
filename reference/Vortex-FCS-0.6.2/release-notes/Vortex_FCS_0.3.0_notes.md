# FS-20 Vortex FCS 0.3.0 — shared asymmetric-load code (the Compass / Su-30 fixes this jet lacked)

Base: 0.2.11 (17 Nov: the STOVL guard work of 0.2.9-0.2.11 is all in; this build carries it unchanged). Minor bump: the
asymmetric-load code is now the shared file all seven FCS mods use. **Status:** compiled against the game's assemblies, benched.
**Not flown.** The STOVL laws, the release / conversion guard and the airbrake are unchanged; the CTOL asymmetric-load limit is not.

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
- **Pitch-cap figure from the coupled check with the nozzles working too** (Su-30 / Compass line, see above). This is the change you
  will feel. 0.2.11 took the held-pitch figure from the held-load sweep with the nozzles' whole travel and a fixed 20 deg/s^2 margin
  whenever the nozzles worked; with 650 kg on the left wingtip that pitch cap sat at 10.4-10.9 deg and was the limit. The coupled
  check's figure (its nose-down with the load held and the roll reserve kept, margin 16-20 deg/s^2 from `AsymPitchMarginEff`) puts the
  pitch cap at 25-28 deg, so the roll-reserve limit holds instead, at its 12 deg floor (`AsymMinAoACap`).
- **Damage gate**, **pitch bias** with the no-TVC pitch-cap floors 7 / 4 deg: without pitch TVC only.
- **Thrust about the real CG** (`AsymThrustCgLoad`), the lateral coupled demand (`CplLateral`), the no-gain pitch skip, the
  differential / yaw nozzles as LP effectors where present, free nozzles at their range from neutral, the low-thrust authority cap
  from the predicted dynamic pressure (`AuthCapPredict`, off).
- Kept as this jet had them: the stab-travel reserve in the LP (`CplStabTravelLp` on, `AsymStabTravel` 40), the capture brake under
  the cap, the gap tighten, `CplStabPeakRule` / `CplTopEdge` off, the hinged-flap snapshot, `AsymCoupledReserveMax` / `NoTvc` 0.8.
- Log: 7 new columns after `pitchMarg`: `asymPitchBias, asymPitchBiasNeg, asymCapPitchNeg, dmgGateHold, cplStopP, cplStopN, cplRef`.
  The STOVL columns are unchanged.

## Bench (0.2.11 vs 0.3.0; `ifly`, ThrustScale 1.12, DragScale 0.885)
- 650 kg on `wingtip_L`, full aft then full roll each way at the limit (1000 m, throttle 0.8):

  | km/h (kt) | limit 0.2.11 | limit 0.3.0 | peak AoA | roll rate +/- deg/s | sideslip max | nz max |
  |---|---|---|---|---|---|---|
  | 250 (135) | 12.0-12.8 | 12.0-16.8 | 13.0 -> 16.2 | +31 / -45 -> +43 / -46 | 5.5 -> 5.9 | 1.93 -> 2.10 |
  | 400 (216) | 10.3-11.5 | 12.0-13.2 | 11.1 -> 13.3 | +79 / -83 -> +57 / -66 | 4.3 -> 6.1 | 3.03 -> 3.34 |
  | 600 (324) | 10.0-10.6 | 12.1-13.1 | 10.6 -> 12.9 | +83 / -103 -> +75 / -80 | 3.6 -> 3.9 | 5.32 -> 6.21 |

  2 deg more AoA for 15-23 deg/s less roll at 400-600 km/h. AoA past the live limit: 250 km/h 0.4 -> 2.2 deg (0.9 s above 0.5 deg; the
  limit itself rises with the roll there, 13.5 deg at that moment), 400 / 600 km/h 0.6 -> 0.7 deg. Toggling `CplLateral`,
  `AsymThrustCgLoad`, `AsymPitchBias`, `AsymPitchNoGainSkip` or `NoVecUncapped` off one at a time does not bring the 0.2.11 limit back
  (only `CplLateral` off moves it: roll +65 / -71 at 400 km/h); the pitch-cap figure is the cause.
- Store release in a pull (500 kg each on `wing2a_L` and `wingtip_L`, dropped at 1 and 3 s, pull at 1.5 / 4.0 / 5.7 s, 330 km/h):
  held AoA after the drops 11.5 -> 12.0, 10.8 -> 12.0, 10.0 -> 12.0 deg (throttle 0.75); bank 8.0 -> 10.1 and 8.2 -> 11.5 deg max.
  Early pull (1.5 s, store still on): AoA past the limit 13.9 -> 6.2 deg max.
- Clean jet (250 / 400 km/h, same manoeuvre): identical.
- STOVL (conversion and decel to hover, hover inputs, the CTOL blend at 40-60 kt, reconversion): every logged column identical to
  0.2.11 apart from the 7 new columns. Airbrake held 3.2-9.2 s at 300 / 700 / 1200 km/h: identical. Conversion guard runs (300 /
  1500 kg on the left wingtip, presses at 1 / 4 / 7 s): identical.
- Clean-jet regression 21 / 21, stop / reversal sweep 33 / 33, corkscrews 21 / 21, damage suite 12 / 12: identical to 0.2.11, summary
  tables line for line.

## Install
- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.3.0; 0.2.11 kept as `Vortex_FS20_FCS.dll.0.2.11.bak`. `meta.json` updated.
- Dev DLL unchanged. Release folder: `C:\Games\NO mods\Vortex-FCS-0.3.0-release`.
