# F-22E FCS 2.9.0 — the push-over's pitch hold owns the stabilators

Base: 2.8.1. **Compiled + bench A/B (identical on/off — the bench never reaches the regime), not flown.** 2.8.1 kept as
`.dll.2.8.1.bak`. One new control-law device (`SweepHoldPitchPrio`), no tuning values touched.

## From the 15:59 flight (2.8.1)
Four released-roll push-overs from 65° AoA. Same shedding power, different rotation:

| t | kt | yaw at push | shed at 65° (capacity / measured) | sideslip |
|---|---|---|---|---|
| 68.5 | 177 | 41 deg/s | 25 / 17 deg/s² | 6.7° |
| 76.4 (roll held) | 176 | 47 | 19 / 14 | 5.7° |
| 46.3 | 224 | 79 | 34 / 28 | 19.5° |
| 87.9 | 225 | 72 | 31 / 22 | 21.2° |

Why the pace did not save the 225 kt ones (46.3 s):
- **65 → 50° AoA:** the shed cap worked. Stick −60 deg/s, command −12…−16 (≈ 30 deg/s of AoA rate: at 3 g / 225 kt the
  flight path curves away at 16 deg/s, the "hold" pitch rate is +16), the airframe tracked it (q −12…−21). The stabs are off
  pitch there by the law's own judgement (`stabPitchFrac` 0: the monotone-clamped stab pitch curve is flat at 65°), pitch is
  TVC-only, and the stabs sat on the roll stop (R −30, L +5…+22).
- **50 → 20° AoA:** the stabs came back to pitch (`stabPitchFrac` 0.9, pitch weight 7.5) but the lateral stop was short by
  130 → 280 deg/s² in roll and 77 → 125 in yaw (`unmetX`/`unmetZ`); the allocator kept them differential (L −8, R +35 at its
  peak; flaperons and ailerons at ±25). The cap tightened each tick as the measured shed fell behind, to **+9 deg/s nose-up**
  at 47.5 s; the nose dropped at **−31 deg/s**. AoA 40 → 20 in 0.4 s against a nose-up command. In weighted least squares a
  280 deg/s² roll residual at weight 1.3 outbids ~45 deg/s² of pitch at weight 7.5, and `RelLatRollPrio` (×3 on roll with
  the stick released and p off its command) made it worse.
- **20 → 5° AoA:** 50 deg/s of yaw rate still on board, fins stalling into the wind, sideslip 2 → 19° in 0.8 s.
The 177 kt push had the same second-half signature (q −43 against +2 commanded at 25–33° AoA) with 22 deg/s of yaw left: 6.6°.

Tom's rule (16:14): don't slow the velocity-vector roll at high AoA, where the airframe's passive stiffness keeps roll and
yaw balanced; when the rotation is too high for the yaw to follow the push, slow the pitch. 2.8.1 asked for that and was
overruled by the allocator.

## Change
**`SweepHoldPitchPrio`** (on): the nose-down counterpart of `OvershootPitchPriority` (1.8.23's nose-up run-away device).
While the AoA-sweep cap has clipped the pitch-rate command on the toward-zero side (`yawSatPitch` < 0 at positive AoA, > 0
at negative) and the nose is moving toward zero faster than the capped command by `SweepHoldErr0` 2 … +`SweepHoldErrSpan`
8 deg/s (above 15–25° AoA), `pitchNeedDn` rises 0 → 1 and the pitch hold takes what the run-away case takes:
- allocator pitch weight 8–15 and roll 0.35–1 instead of 1–7.5 / 1.3–4.5 (`t3` → 0);
- `RelLatRollPrio` off (×(1 − pitchNeedDn));
- the honest stab model (`honestGate`), the nozzles freed (`band0`, `TvcBudget`), the stab-vs-nozzle pitch fight off.
It does **not** feed the coupling roll gate (`gateC` reads `pitchNeedUp`): the rotation is not cut, the stabs are taken back
for the hold. The roll stop keeps the flaperons, ailerons, rudders and TVC, so with the stick released the rotation runs on
for a moment longer while the AoA holds — the "slow down for a bit" Tom accepts on the push-over.

Replayed over the 15:59 log the gate would have been up 46.7–47.8 s and 88.3–89.5 s (the two 20° pushes), 69.3–69.8 and
76.8–77.8 (the two good ones, briefly), and in the 15:02 / 15:22 / 15:29 logs only in ≤ 0.4 s episodes inside push-overs
under a roll. It cannot help in the 65 → 55° part (stabs off pitch by `stabPitchFrac`), and it is a priority, not more
authority: the stab still slews −30 → +35 at its rate limit (0.6 s in the log). Expect the 225 kt case to improve, not
vanish; if it still runs past the margin the remaining option is the one Tom declined for now — not building the rotation.

Telemetry: `pitchNeedDn` column after `pitchNeed` (game and bench).

## Bench (sideslip ~2× high)
Reversal suite, corkscrew entries, 18-case regression set and the utilisation invariant: **identical** with the device on
and off (the bench cannot make the leaf's yaw rate at the push, so the gate never fires there). A first cut gated on the raw
sign of `yawSatPitch` fired on the push to −20° AoA under a roll (the cap clipping the sweep that *grows* |AoA|, pitch merely
lagging): 1.3° more sideslip in the pull-through after — fixed by gating on sign(AoA) × `yawSatPitch`.

## Carried over, unchanged
The 28° / 300 kt roll rate (2.7.6's 59 vs 70 now; `RollPaceRateBlend` ≈ 0.53 is the knob, the cap reading ~14 there is the
real item), the rate cap's reversal model and the window holding reversals at zero (handoff §4), the old lateral governor
code still in place.
