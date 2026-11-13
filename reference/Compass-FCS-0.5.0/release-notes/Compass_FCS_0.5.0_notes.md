# T/A-30 Compass FCS 0.5.0 (12 Nov 2026): flying backwards, the stick and pedals turn the nose the way they point

Base: 0.4.2. Minor bump: a lateral control-law change, in reversed flight only. The F-22E 4.3.0 law, ported.

Tom, 12 Nov: "remember this rule from now on for velocity vector roll when flying backwards" / "the 2d canted is towards the tvc vector roll. So do the rest for all of them".

(Condensed for the kit: the shared law text is given once, in the F-22E 4.3.0 notes.)

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,370,112 bytes, SHA-1 61e3bc9839a584c202bdb88ed0591fda74646a04. The 0.4.2 source rebuilt gives the released 0.4.2 DLL byte for byte.
- Benched: full suite against a 0.4.2 bench built from the same config; MPO tail-slide cases.
- **Not run on the game's Mono** (no Wine set-up in that session). **Not flown.**

Your MPO setting is VelocityVectorRoll, so the cases below are your setting.

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
| A | 115 -> 128 deg, 145 km/h | right stick | -14.8 / +19.8 -> +10.0 / +16.7 | -8.2 / -2.1 -> 9.6 / 17.2 | 8.8 / -7.4 -> 25.1 / 7.1 |
| B | 135 -> 145 deg, 186 km/h | left stick | -20.6 / +16.8 -> -20.7 / -5.2 | -18.7 / 12.5 -> -19.5 / -13.3 | 2.5 / 1.4 -> 32.3 / -20.9 |
| C | 115 -> 128 deg, 145 km/h | left pedal | +10.9 / -14.6 -> -5.7 / -13.2 | 5.4 / 0.4 -> -6.7 / -15.4 | 6.2 / 5.2 -> 19.7 / -1.7 |
| F | 135 -> 145 deg, 186 km/h | right pedal | +21.2 / -17.1 -> +17.9 / +1.3 | 19.2 / -13.0 -> 17.0 / 9.8 | 2.6 / -1.2 -> 27.4 / 5.2 |

Roll and nose follow the input in every case. The cost is sideslip: peaks of 20-32 deg against 3-9; in B (186 km/h) still -21 deg 3 s after the release.

## Regression
Suite: 155 / 155 per-case files identical, timing aside. Two damage cases (c24, c28) crash in the bench in both versions, as before.

## Files
`FcsController.cs` (`LateralLaw`: `num` = |cos AoA|, `numPhys` for the sideslip kinematics and the hold's yaw, `revS` = 1; the pace window's `cosH`; the hold weights; the MPO-separate `rM`; `LatAxisAlpha()`), `Plugin.cs` (`ReversedFlowNoseAxis`), `FcsStandalonePlugin.cs` (version), bench `an/revflow_jet.py`.
