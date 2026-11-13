# KR-67 Ifrit FCS 1.3.0 (12 Nov 2026): flying backwards, the stick and pedals turn the nose the way they point

Base: 1.2.0. Minor bump: a lateral control-law change, in reversed flight only. The F-22E 4.3.0 law, ported.

Tom, 12 Nov: "remember this rule from now on for velocity vector roll when flying backwards" / "the 2d canted is towards the tvc vector roll. So do the rest for all of them".

(Condensed for the kit: the shared law text is given once, in the F-22E 4.3.0 notes.)

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,302,016 bytes, SHA-1 0259e23d7549e5bcf05748c157c6a611c939359f. The 1.2.0 source rebuilt gives the released 1.2.0 DLL byte for byte.
- Benched: full suite against a 1.2.0 bench built from the same config; MPO tail-slide cases.
- **Not run on the game's Mono** (no Wine set-up in that session). **Not flown.**

Your MPO setting is SeparateRollYaw (stick = body roll, pedal = body yaw), so in MPO nothing changes. The change applies to the coupled law (Stability Assist on) and to MPO's VelocityVectorRoll; the table is MPO with VelocityVectorRoll.

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
| A | 115 -> 129 deg, 100 km/h | right stick | -4.7 / +5.7 -> -1.3 / +2.5 | -4.3 / 3.5 -> -1.5 / 1.7 | 1.7 / 0.7 -> 5.0 / -0.6 |
| B | 135 -> 144 deg, 131 km/h | left stick | -18.6 / +13.7 -> -10.0 / +1.6 | -13.2 / 3.6 -> -10.1 / -2.7 | 4.7 / -1.8 -> 12.7 / 2.7 |
| C | 115 -> 129 deg, 100 km/h | left pedal | +4.7 / -5.7 -> -2.0 / -5.1 | 4.4 / -3.3 -> -3.0 / -4.1 | 1.9 / -0.3 -> 8.2 / 1.0 |
| F | 135 -> 144 deg, 131 km/h | right pedal | +18.5 / -13.7 -> +13.5 / +2.9 | 13.2 / -3.6 -> 14.6 / 6.9 | 4.8 / 2.1 -> 19.1 / -2.7 |

The nose follows the input in every case; in A the roll is still slightly against the stick (-1.5 deg/s) because the sideslip hold (65 % with the low-TAS fade at 28 m/s) takes the yaw back. Sideslip back within 3 deg by the end.

## Regression
Suite: 150 / 159 per-case files identical (timing aside). The 9 that differ are 3 damage cases (both ruddervators gone, push-overs at 600 km/h, plus c24), differing once AoA passes 90 deg; the push-overs depart in both versions.

## Files
`FcsController.cs` (`LateralLaw`: `num` = |cos AoA|, `numPhys` for the sideslip kinematics and the hold's yaw, `revS` = 1; the pace window's `cosH`; the hold weights; the MPO-separate `rM`; `LatAxisAlpha()`), `Plugin.cs` (`ReversedFlowNoseAxis`), `FcsStandalonePlugin.cs` (version), bench `an/revflow_jet.py`.
