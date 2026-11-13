# FS-12 Revoker FCS 1.4.0 (12 Nov 2026): flying backwards, the stick and pedals turn the nose the way they point

Base: 1.3.1. Minor bump: a lateral control-law change, in reversed flight only. The F-22E 4.3.0 law, ported.

Tom, 12 Nov: "remember this rule from now on for velocity vector roll when flying backwards" / "the 2d canted is towards the tvc vector roll. So do the rest for all of them".

(Condensed for the kit: the shared law text is given once, in the F-22E 4.3.0 notes.)

**Status:**
- Compiled against the game's assemblies. Built twice, same bytes: 1,348,608 bytes, SHA-1 e180563c3bfdfebcef3da9521aa83a1182ab0fce. The 1.3.1 source rebuilt gives the released 1.3.1 DLL byte for byte.
- Benched: full suite against a 1.3.1 bench built from the same config; MPO tail-slide cases.
- **Not run on the game's Mono** (no Wine set-up in that session). **Not flown.**

Your MPO setting is SeparateRollYaw; on the Revoker that is stick = velocity-vector roll (changed) and pedal = body yaw (unchanged). In that mode, right stick at 115-128 deg / 105 km/h: pCmd/rCmd -12.9/+16.3 -> +4.1/+10.5; left stick at 135-143 deg: +20.6/-15.3 (roll right, nose left) -> -10.0/-1.1. That mode holds sideslip only with the stick in, so after the release it stays where the turn left it (29.9 / 24 deg, were 4.4 / 4.5); the pedal cases did the same on 1.3.1. The table is MPO with VelocityVectorRoll.

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
| A | 115 -> 128 deg, 105 km/h | right stick | -9.0 / +10.0 -> +4.4 / +10.0 | -8.9 / 7.7 -> 7.1 / 15.7 | 1.8 / -0.6 -> 19.5 / 0.2 |
| B | 135 -> 144 deg, 138 km/h | left stick | -14.4 / +10.2 -> -11.9 / -3.6 | -14.1 / 10.1 -> -13.6 / -6.1 | 2.8 / 2.4 -> 17.2 / 0.2 |
| C | 115 -> 128 deg, 105 km/h | left pedal | +9.1 / -10.0 -> -1.0 / -7.4 | 9.0 / -8.0 -> -6.4 / -14.9 | 1.8 / 0.6 -> 18.1 / 0.0 |
| F | 135 -> 144 deg, 138 km/h | right pedal | +14.4 / -10.2 -> +10.9 / +2.1 | 14.1 / -10.1 -> 12.2 / 3.4 | 2.9 / -2.5 -> 15.0 / -0.3 |

Roll and nose follow the input in every case; sideslip back under 1 deg by the end.

## Regression
Suite (reg, sweep, corkscrews, damage): 159 / 159 per-case files identical, timing aside.

## Files
`FcsController.cs` (`LateralLaw`: `num` = |cos AoA|, `numPhys` for the sideslip kinematics and the hold's yaw, `revS` = 1; the pace window's `cosH`; the hold weights; the MPO-separate `rM`; `LatAxisAlpha()`), `Plugin.cs` (`ReversedFlowNoseAxis`), `FcsStandalonePlugin.cs` (version), bench `an/revflow_jet.py`.
