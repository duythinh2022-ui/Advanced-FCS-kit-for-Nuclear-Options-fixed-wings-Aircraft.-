# FS-3 Ternion FCS 0.4.0 (12 Nov 2026): flying backwards, the stick and pedals turn the nose the way they point

Base: 0.3.2. Minor bump: a lateral control-law change, in reversed flight only. The F-22E 4.3.0 law, ported.

Tom, 12 Nov: "remember this rule from now on for velocity vector roll when flying backwards" / "the 2d canted is towards the tvc vector roll. So do the rest for all of them".

(Condensed for the kit: the shared law text is given once, in the F-22E 4.3.0 notes.)

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,405,952 bytes, SHA-1 8122cf315413163d34b737ea776e652c4027c5ac. The 0.3.2 source rebuilt gives the released 0.3.2 DLL byte for byte.
- Benched: full suite against a 0.3.2 bench built from the same config; MPO tail-slide cases.
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
| A | 115 -> 125 deg, 100 km/h | right stick | -5.4 / +8.1 -> +1.3 / +5.7 | -3.6 / 0.5 -> 1.7 / 3.6 | 2.6 / -1.6 -> 5.8 / -0.1 |
| B | 135 -> 143 deg, 132 km/h | left stick | -13.2 / +10.3 -> -13.6 / -5.7 | -12.1 / 8.3 -> -12.5 / -9.8 | 3.3 / 2.9 -> 20.9 / -6.5 |
| C | 115 -> 125 deg, 100 km/h | left pedal | +5.5 / -8.3 -> -1.2 / -5.6 | 3.7 / -0.1 -> -1.9 / -3.1 | 2.9 / 2.1 -> 5.7 / 0.1 |
| F | 135 -> 143 deg, 132 km/h | right pedal | +13.6 / -10.5 -> +10.9 / +2.2 | 12.2 / -7.7 -> 9.5 / 8.6 | 2.1 / -1.8 -> 18.7 / 3.4 |

Roll and nose follow the input in every case; sideslip peaks up to 21 deg against 3, under 7 by the end.

## Regression
Suite: 155 / 155 per-case files identical, timing aside. Two damage cases (c24, c28) crash in the bench in both versions, as before.

## Files
`FcsController.cs` (`LateralLaw`: `num` = |cos AoA|, `numPhys` for the sideslip kinematics and the hold's yaw, `revS` = 1; the pace window's `cosH`; the hold weights; the MPO-separate `rM`; `LatAxisAlpha()`), `Plugin.cs` (`ReversedFlowNoseAxis`), `FcsStandalonePlugin.cs` (version), bench `an/revflow_jet.py`.
