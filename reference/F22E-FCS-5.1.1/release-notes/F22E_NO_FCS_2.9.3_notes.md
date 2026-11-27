# F-22E FCS 2.9.3 — the sweep cap is led across the zero crossing instead of stepping

Base: 2.9.2. **Compiled + bench-checked (zero-crossing cases 0.3–2.6° better, command steps at the crossing halved), not
flown.** 2.9.2 kept as `.dll.2.9.2.bak`. One new static (`SweepCrossLead` 0.3 s), no tuning values touched.

## From the 16:59 / 17:03 flights (2.9.2)
Released-roll push-overs from 65°: 175 kt / 46 deg/s → 5.1°; 222 kt / 61 → **7.9°**; 192 kt / 58 → 6.0°. The hold did its job:
`sweepWin` 1, `pitchNeedDn` to 0.9–1.0, no nose-up overshoot at the release (max q between 30° and 10° AoA −17…−44 against
+10 in the 16:37 log), hitch gone — Tom: "everything else seems fine". No push from ≥ 72 deg/s in these two logs, so the
215 kt / 75 case is still unflown on 2.9.2.

Held-roll push-overs (the "still a bit rough"): 150 kt / 45 → 5.0°, 128 kt / 44 → 4.8° — the sideslip is fine; the roughness
is at the **zero crossing**. 17:03 log, 29.7 s (136 kt, 35 deg/s of roll held): the pitch command went −44 → −24 deg/s in
0.1 s as the AoA crossed zero, q −42 → −30; at 43.0 s −23.5 → −17. Cause: the sweep toward zero is capped by the shed / sweep
rule only (2.6.5 — a shrinking yaw demand is served by unloading, free), and past zero the demand grows again and the
trade's share (`tradeAlpha` 17–23 deg/s there, against 11–16 deg/s² of measured yaw capacity at 136 kt) caps it. Two rules,
switched at α = 0, while the yaw acceleration the crossing needs — pS·cos α·α̇ — is the same on both sides.

## Change
`SweepCrossLead` 0.3 s: while sweeping toward zero, the |AoA|-shrinking cap is blended toward the far side's cap (the trade's
share, or the pull cap on a pull-through) over the AoA the sweep will cross within 0.3 s (w = 1 − |α| / (|α̇|·0.3)). At −42
deg/s that starts 12.6° before the crossing: the −44 → −24 step becomes a 0.3 s ramp. 0 restores the 2.6.5 step.

## Bench (sideslip ~2× high)
Reversal suite, corkscrew entries, utilisation invariant: identical. Regression set: push+roll 200 8.0 → **5.4**, push+roll 300
3.2 → 3.2, pull-then-pushover 350 4.6 → 4.3, 450 6.3 → 5.5, push-then-pullthrough 350 4.8 → 4.4, 450 8.2 → 6.9, pullthrough
released at zero 6.8 → 6.9; the rest unchanged. Largest per-tick pitch-command step within ±6° of zero: 10.7 → 4.2, 10.6 →
8.0, 16.9 → 10.3, 9.2 → 9.2, 16.9 → 10.5 deg/s (the remaining steps in the pull-throughs are the stick reversal itself).

## Also seen at the crossing, not touched
The pace window latched at the crossing in both held-roll pushes (`paceHold` −19…−20, roll target −35 → −14 for 0.4 s) on
13–17 deg/s² of yaw unmet at 136 kt. That is §4 (b) of the handoff — the window — and the roll side's next item, with the
rate cap's reversal model.
