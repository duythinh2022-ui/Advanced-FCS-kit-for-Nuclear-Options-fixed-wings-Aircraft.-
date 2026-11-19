# F-22E FCS 2.19.0 — the window paces where the rudders are not the yaw axis; the nose-hold moves to where the stabs are free

Base: 2.18.0. **Compiled + bench-checked, not flown.** 2.18.0 kept as `.dll.2.18.0.bak`. Two law changes, both re-placements
of 2.18.0 devices on the physics the 00:09 / 00:11 logs showed. No tuning values touched. Statics: `PaceHoldPacedByRudder`
(on), `SweepHoldRate` 12 deg/s, `SweepHoldRateBlend` 6, `SweepHoldStabFrac` 0.25; `SweepHoldFadeMargin` / `SweepHoldBlend` gone.

## The 00:09 and 00:11 flights (2.18.0, both fins off) — "still hesitant", "pitch sticky after the roll is stopped"
**Reversal, 00:09 log, 82.4 s (65°, 140 kt, 40 deg/s):** the body roll braked to zero in 0.6 s, then the target sat at **0 for
3.8 s** while the yaw decayed at 8 deg/s² (capacity read 5–11) — one stab at −21, the other at 0, flaperons and ailerons at the
roll stops. The band put the target on the old side (the yaw still implied −34), the "never a roll against the one in progress"
rule made that 0, and the roll loop held the body roll at 0 against the sideslip-driven roll. Without fins the yaw effector at
65° *is* the stabs' differential, which rolls the jet as it yaws; a roll loop fighting that roll gets the allocator's priority
(roll weight 1.5 vs yaw 8) and the yaw brake gets what is left. In the 23:49 flight's 62 s reversal the roll target was already
on the new side, both stabs went to −30 / +35 together and the yaw braked at **24 deg/s²** — same jet, same AoA, three times
the yaw. "The roll waits for the yaw" is wrong where the yaw effectors are the roll effectors.

**Sticky pitch, 00:09 log 52.9–54.4 s and 00:11 log 97–99.6 s:** stick neutral, roll stopped, 7–10 deg/s of *coordinated*
residual rotation, sideslip ≈ 0 — the 2.18.0 nose-hold held the AoA at 48° for 1.5–2.5 s because it counted every deg/s of
yaw as excess. And **00:11 log 107 s** (held push, roll released at 109.5): the nose held at 50° as designed, but at 50° the
stabs are back on pitch holding the nose up and nothing is left for the yaw — r stayed 29–34 for four seconds, sideslip to
−46°. The stabs are free for yaw only while they are off pitch (`stabPitchFrac` ≈ 0, pitch on the nozzles: ~60–65°).

## Changes
1. **Window paced by rudder weight** (`PaceHoldPacedByRudder`): the latched window's target is `Lerp(paced, band, rudW)`,
   `rudW` = rudderAuth × fin health — the same weight the estimator uses for whether the yaw capacity is the rudders' or the
   stabs'. With the rudders as the yaw axis (mid AoA, fins on) the band holds the roll at the yaw's rotation as before; where
   they are faded or gone the target moves toward the demand at the yaw's measured capacity / sin α, so the roll goes with
   the stab differential instead of against it. The clean jet changes only above the fade (65° reversals 0.4–0.5 s quicker).
2. **Nose-hold re-placed:** the AoA-rate cap goes to zero while the stabs are off pitch (`stabPitchFrac` < 0.25) and the
   velocity-roll rotation exceeds 12 deg/s (`SweepHoldRate`; ~0.7° of sideslip per deg/s in four flights), scaled by the fin
   loss. The 2.18.0 credit removal is undone (the credit reads the stick's target as in 2.10.0). Roll held: the nose stays at
   60–65° until the roll is released; released: until the rotation is under 12 deg/s; a slow coordinated residual no longer
   holds the nose. With one fin or both: off.

## Bench (sideslip ~2× high), 2.18.0 → 2.19.0
Clean regression set: identical except the 65° releases' hold 0.9 / 1.2 → 0. Reversals: fullaft 500 time-to-zero 2.02 → 1.58 s,
300 2.38 → 1.87, sideslip 12.3 → 11.4 / 5.4 → 6.3; mid-AoA cases unchanged. Corkscrew entries unchanged. Utilisation 4 FAIL / 9
as before. Damage suite — **no-fin push-overs 29.6 / 12.8 / 60.7 / 48.6 → 11.5 / 11.5 / 9.9 / 9.9°, none departing** (held:
nose stays at 61–63°; released: nose to 8° / −26°); no-fin leaf 10 / 11 / 9 / 10 → 10 / 13 / 13 / 11.5 (the bench's no-fin yaw
shed is a third of the flight's); one fin ±0; fins at 8 HP rolling pull 18 → 21; fins + flaperon at 480 kt 38 → 41 (departed in
flight too).

## What to expect without fins
Reversals at 65°: the roll goes with the yaw brake — both stabs to the stops together, ~1–1.5 s to cross, sideslip a few
degrees more than the 2.17.0 leaf's 4.7°. Push-overs: with the roll held the nose stays at 60–65°; released, it waits there
until the rotation is under ~12 deg/s (1–3 s), then comes down with the roll following. A stopped jet with a slow residual
rotation pitches normally.

## For the next log
Both fins off: reversals and stops at 65°; a released push with 30–45 deg/s on (nose waits at 60–65°, then down); a held push
(nose stays up; release the roll and watch); pitch inputs after a stop (should be immediate). Clean: one corkscrew entry at
300–350 kt and one 65° reversal. `LogOutput.log` must say 2.19.0.
