# F-22E FCS 2.7.4 — capacity ceiling at high AoA

Base: 2.7.3. **Bench-only, not flown.** 2.7.3 kept as `.dll.2.7.3.bak`.

## What the 11:30 log (2.7.3) showed
- **Push-over from 64° / 210 kt with the roll released (116.1 s): 22° of sideslip.** The leftover yaw (r = −57 deg/s, the velocity-vector roll at 64°) has to be shed before the AoA reaches zero; the shed cap that paces the push read 2.7.3's airframe capacity, 143–162 deg/s², where the allocator was reporting 26–129 unmet and 2.6.x's `aYaw × (1 − yawSat derate)` would have read ~12–25. At 64° AoA the tables overstate the stabs' yaw 3–4× (2.5.12), and the committed increment is read from those tables. **Fix:** every measured capacity (yaw and roll, both sides) is bounded above by the allocator's own authority less its saturation derate — the 2.5.12 ceiling. It can now only read lower than 2.7.2's figure, never higher. (The held-roll push-overs — 237 / 251 s, 3–5° — were within the margin.)
- **The re-pull hitch** (112.7 s, 167.8 s: pull released while rolling, pulled again a moment later; `qCmd` 24 → 12 → 16 → 21 → 31 → 44): the trade's *pitch share* (`tradeAlpha` 8.5–14.5 deg/s), which holds the pull until the roll — still at 60–190 deg/s — has slewed down to the trade's limit. I tried it off in measured mode: the bench then makes 13–17° on pull + roll reversals and overshoots the AoA, so the measured AoA-rise cap alone does not yet cover the AoA rise under a roll. **It stays; the hitch on a re-pull with a fast roll in progress stays with it.** The honest next step is one accounting for both (the trade spends `PullTradeFreeK` 0.35 of the capacity, the AoA-rise cap spends capacity − roll build at `StopSlipPitchK` 0.8) — not done.
- **The held-roll pull through 55 → 66° (181.8 s)**: `qCmd` 59 → 12 → 30 with `yawSatPitch` and `tradeAlpha` both 0 — the AoA law approaching the 65° limit with `alphaCmd` (45 → 64 at ~20 deg/s) lagging the real AoA; the pitch backs off as the AoA overshoots the command, then resumes as the command catches up. Not the governor; it predates 2.7.

## Bench (2× pessimistic on sideslip)
Identical to 2.7.3 everywhere except the 160 km/h falling-leaf reversal: 1.03 → 2.83 s to zero (2.6.11: 2.28; 2.7.3's 1.03 came from the same overstated capacity that made the 22° push-over). The bench has no scenario for the released-roll push-over from 64°; the fix is by construction (the ceiling is 2.6.x's figure) and the next log's `capZN` in that manoeuvre is the check.

## What to fly
The released-roll push-over from 60°+; the roll-held push-over; the same entries.
