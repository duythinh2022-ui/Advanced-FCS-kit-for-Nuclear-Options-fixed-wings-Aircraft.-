# F-22E FCS 2.9.4 — the push-over pace gets a sideslip allowance (`PushoverSideslip`)

Base: 2.9.3. **Compiled + bench-checked, not flown.** 2.9.3 kept as `.dll.2.9.3.bak`. One new player key, section 6; no tuning
values changed.

## From the 17:15 flight (2.9.3)
Released-roll push-overs from 65°: 204 kt / 66 deg/s of yaw → 5.3°, 207 / 62 → 8.0°, 202 / 65 → 11.1°, 143 / 41 → 4.3°,
139 / 43 → 3.0°; 65 → 5° in 1.7–2.1 s, the command held to −16…−26 against the stick's −60 the whole way (`yawSatPitch`
−32…−43 median). Held-roll pushes 3.2 / 4.7° with the crossing smooth. Tom: "sideslip control is a little too tight, the
nose-down is too slow when the yaw rate is high; I don't mind a few more degrees. At lower yaw rate it is faster but still a
little slower than before."

The shed cap since 2.8.1 paces the sweep so that *all* the excess yaw rate (above `SweepShedDead` 5 deg/s) is shed before
the AoA reaches zero. That is the "too tight": it leaves the rudders nothing to do at the bottom.

## Change
**`PushoverSideslip`** (player key, section 6, default −1 = `RollStopSideslip`, 0–20°): the sideslip the push may make at the
crossing from the yaw rate still on board. Killing a yaw rate r at the measured shed a costs r²/2a of sideslip once the AoA
is low, so r_left = √(2·a·allowance) deg/s may remain and the pace sheds only the excess above it. At the measured shed of
15–25 deg/s² in these pushes and the 4° default: r_left 11–14 deg/s — the 66 deg/s push sheds 48 instead of 61 before zero
(pace ×1.27), the 43 deg/s push 25 instead of 38 (×1.5). Set it higher for faster still: 8° → r_left 16–20 deg/s. 0 = 2.9.3.
Expect roughly the allowance on top of what the pushes make now (they already make 3–11° with "all shed before zero",
because the shed collapses once the fins stall past ~10°); if the 65 deg/s pushes go past ~15° at 4, lower it, not the
roll.

## Bench (sideslip ~2× high)
Reversal suite, corkscrew entries, utilisation invariant: identical. Regression set: one case moves — the pull-through with
the roll released at zero, 6.8 → 7.5° (the cap's cut on that sweep 44.7 → 13.4 deg/s: the allowance letting the nose through).
