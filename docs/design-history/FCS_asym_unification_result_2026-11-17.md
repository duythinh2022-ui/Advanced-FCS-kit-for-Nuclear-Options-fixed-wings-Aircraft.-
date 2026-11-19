# One asymmetric-load code for all seven FCS mods — result (17 Nov 2026)

Tom, 17 Nov 10:08: "make all of them use the same thing"; 10:26: option 3 (the same code everywhere, taken from the Su-30;
the Su-30's "remaining useful authority" design on the Su-30 only; tuned numbers that differ for no airframe reason stay per jet).
**Status on the day:** all seven compiled against the game's assemblies, benched old vs new, released and installed. None flown.

## Versions

| Mod | Was | Now |
|---|---|---|
| Su-30VXN | 0.6.2 | 0.6.3 |
| T/A-30 Compass | 0.5.0 | 0.5.1 |
| FS-3 Ternion | 0.4.0 | 0.4.1 |
| FS-12 Revoker | 1.4.2 | 1.5.0 |
| KR-67 Ifrit | 1.3.0 | 1.4.0 |
| F-22E | 4.3.0 | 4.4.0 |
| FS-20 Vortex | 0.2.11 | 0.3.0 |

## How it is built
- `AsymShared.cs` (new), `AsymCap.cs`, `CoupledCap.cs`: the same text in all seven, only the `namespace` line differs.
  `FcsController` is `partial`; the shared file holds the asymmetric-load methods and their state.
- Check: `tools/asym_shared/checkshared.py` (in every tree): sha256 of the namespace-normalised files. On the day: AsymShared
  `62f3250027774425`, AsymCap `f1b8d2915c6e087c`, CoupledCap `6f66ef4087ade420` in all seven. Later the same day the engine-out
  zero-thrust helpers went into AsymShared (`07b1e78b2722f37a`, Ifrit 1.5.0 / Su-30 0.6.4 / F-22E 4.5.0 / Ternion 0.4.2 / Vortex
  0.3.1); the Revoker and Compass were not re-released for it, since `EngineOutZeroThrust` is a constant `false` on both
  (MANUAL §2.19).
- Per-jet settings in each jet's own files:

| Jet | CplRefAuth / CplKneeOn0 | CplStabPeakRule / CplTopEdge | CplStabTravelLp | AsymStabTravel | AsymStopTime |
|---|---|---|---|---|---|
| Su-30 | on | on | off | 40 | 0.8 |
| Compass, Ternion | off | off | off | 40 | 0 |
| Revoker, Vortex | off | off | on | 40 | 0 |
| Ifrit, F-22E | off | on | off | 0 | 0 |

  The F-22E keeps `AsymCoupledCheck` off as shipped in 4.2.0.

## Bench, old vs new (every logged column)
- **Su-30:** matrix 31/31, loaded reversals 88/88, regression / sweep / corkscrews identical.
- **Compass:** matrix 24/24, loaded reversals 264/264, regression / sweep / corkscrews identical; damage suite 4/50 differ (largest
  20.5 -> 20.6 deg).
- **Ternion:** 21/24 matrix cases within 1 deg; three differ in one figure each (peak roll 40.9 -> 43.8 deg/s; idle-pull AoA past the
  limit 4.3 -> 2.1 deg; IAS 125 -> 128). Cause: the sweep chunk sizing, now latched at sweep start.
- **Revoker:** 18/25 matrix within 1.5 deg; 1160 kg 300 km/h AoA past the limit 3.5 -> 6.8 deg (gap tighten). Loaded reversals:
  mean sideslip 1.90 -> 2.00 deg, mean peak AoA 45.3 -> 46.4 deg. Clean sets and damage table identical.
- **Ifrit:** 16/25 matrix within 1.5 deg; throttle-chop AoA past the live limit 3.9 -> 8.9 deg (gap tighten; AoA itself lower).
  Damage suite: 18/52 differ, all from gap tighten (one ruddervator lost, leaf at 370 km/h: sideslip 7.9 -> 10.6 deg).
- **F-22E:** as shipped 23/24 matrix identical (one roll case within 2 deg/s), loaded reversals 88/88, regression, corkscrews,
  damage identical. With the coupled check on: 10/24 identical, 19 within 1 deg; the 1000 kg pulls hunt on both builds.
- **Vortex:** with 650 kg on the left wingtip the CTOL limit is 12-13 deg (0.2.11: 10-11.5) and the rolls at the limit +43/-46,
  +57/-66, +75/-80 deg/s at 250 / 400 / 600 km/h (0.2.11: +31/-45, +79/-83, +83/-103). Clean jet, STOVL set, airbrake, conversion
  guard: identical.

## What moved the four jets (Revoker, Ifrit, F-22E, Vortex)
1. **Pitch-cap figure from the coupled check with the nozzles working.** Those jets used it only without the nozzles
   (`cplPitchGate`); with them working they took the held-load sweep's figure (nozzles' whole travel, fixed 20 deg/s^2 margin).
   On the Vortex with 650 kg on a wingtip this pitch cap was the limit at 10.4-10.9 deg; now it is 25-28 deg and the roll-reserve
   limit holds at its floor.
2. **Gap tighten** (Revoker, Ifrit; the F-22E and Vortex had it): a limit that has to come down a long way comes down up to 4x
   faster. It produces the "AoA past the live limit" increases on throttle chops and the extra 1-3 deg of sideslip after a
   ruddervator loss on the Ifrit.
3. The no-TVC pieces (damage gate, pitch bias, capture brake) act only with the nozzles locked or thrust too low for them.

## Open items on the day
- Vortex 250 km/h loaded roll: AoA up to 2.2 deg past the live limit for 0.9 s; the limit itself moves with the roll.
- F-22E coupled check still off by default; with it on the 1000 kg pulls hunt (5-7 raw-limit jumps) on 4.3.0 and 4.4.0 alike.
- The Su-30 roll-stop block (`AsymStopTime` / `CplRefAuth`, `cplStopP` / `cplStopN`) is in the Su-30's and Vortex's lateral law
  only; it would be inert on the others under option 3, so it was not ported into their laws.
- The "AoA past the limit" figure counts against the live limit; when gap tighten drops the limit quickly the figure grows while
  the AoA itself is lower. Read both.
- The Ifrit ruddervator-loss sideslip cost of gap tighten: whether it stays on for the Ifrit is the author's call.
