# Su-30VXN FCS 0.6.0 (12 Nov 2026): flying backwards, the stick and pedals turn the nose the way they point

Base: 0.5.7. Minor bump: a lateral control-law change, in reversed flight only. The F-22E 4.3.0 law, ported.

Tom, 12 Nov: "remember this rule from now on for velocity vector roll when flying backwards" / "the 2d canted is towards the tvc vector roll. So do the rest for all of them".

(Condensed for the kit: the shared law text is given once, in the F-22E 4.3.0 notes.)

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,526,272 bytes, SHA-1 bf65f3ec2433643fa907878b4322fd750a90816b. The 0.5.7 source rebuilt gives the released 0.5.7 DLL byte for byte.
- Benched: full suite against a 0.5.7 bench built from the same config; MPO tail-slide cases.
- **Not run on the game's Mono** (no Wine set-up in that session). **Not flown.**

Your MPO setting is VelocityVectorRoll. Past the AoA limit in MPO (and in the low-speed band) the stick turns about the canted nozzle differential's axis (the TVC vector roll), left as it is, except that the sense blend used to flip that rotation too: at 135-150 deg left stick turned the jet right. The rotation's start angle (`ang`) is now the mirrored axis. The TVC vector roll's sideslip hold stands aside by design, so the sideslip stays after the release (case A did the same on 0.5.7).

## Change (`ReversedFlowNoseAxis`, dev, on): the F-22E 4.3.0 law
The shared text is in `reference/F22E-FCS-4.3.0/release-notes/F22E_NO_FCS_4.3.0_notes.md` and README §8.24. In short: the
velocity-vector roll's body-roll share is |cos AoA| instead of cos AoA (yaw stays sin AoA), replacing the
ReversedFlowSenseStart/End blend; the pace window and capacity estimator use the same axis (`LatAxisAlpha()`); the sideslip
hold no longer fades out from 100 to 120 deg AoA (its low-TAS fade stays); forward flight, the TVC vector roll and the MPO
separate-axes body roll / body yaw are unchanged. Dev key `3. Lateral-directional / ReversedFlowNoseAxis` (true; false = the
previous release). No migration.

Bench cases: climb at 60 deg from 300 km/h, throttle 0.25, stick centred, then 1.5 s of input (`bench/an/revflow_jet.py`).
Commands and rates at the end of the input, old -> new; positive = right roll / nose right; sideslip peak in the 4.5 s from
the input / value at the end.

| Case | AoA, IAS | Input | pCmd / rCmd | p / r measured | Sideslip peak / end |
|---|---|---|---|---|---|
| A | 115 -> 125 deg, 111 km/h | right stick | 11.2 / 8.7 -> 13.6 / 10.6 | 9.2 / 9.1 -> 9.6 / 9.3 | 21.6 / 21.6 -> 24.0 / 24.0 |
| B | 135 -> 141 deg, 150 km/h | left stick | +9.2 / +7.3 -> -17.0 / -13.3 | 6.7 / -0.8 -> -14.7 / -9.1 | 4.8 / 4.7 -> 29.9 / -29.9 |
| C | 115 -> 126 deg, 111 km/h | left pedal | -11.0 / -8.5 -> -13.6 / -10.6 | -9.2 / -9.0 -> -9.5 / -9.1 | 21.1 -> 24.0 |
| F | 135 -> 141 deg, 150 km/h | right pedal | -9.2 / -7.3 -> +17.0 / +13.2 | -6.8 / 0.9 -> 14.9 / 9.0 | 4.6 -> 30.8 |

A / C were already right on 0.5.7 (the blend was +1 below 120 deg); B / F went the wrong way and now follow the input.

## Regression
Suite (reg, sweep, corkscrews): 99 / 99 per-case files identical, timing aside. The damage suite does not run on this bench (no Su-30 damage data).

## Files
`FcsController.cs` (`LateralLaw`: `num` = |cos AoA|, `numPhys` for the sideslip kinematics and the hold's yaw, `revS` = 1; the pace window's `cosH`; the hold weights; the MPO-separate `rM`; `LatAxisAlpha()`), `Plugin.cs` (`ReversedFlowNoseAxis`), `FcsStandalonePlugin.cs` (version), bench `an/revflow_jet.py`.
