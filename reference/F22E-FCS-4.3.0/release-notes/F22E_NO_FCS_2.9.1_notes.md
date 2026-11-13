# F-22E FCS 2.9.1 — the pitch hold's stabilators are rate-bounded, so the increment is spread, not banked

Base: 2.9.0. **Compiled + bench A/B identical (the bench never reaches the regime), not flown.** 2.9.0 kept as
`.dll.2.9.0.bak`. One line in the law: the existing `ReleasedStabRateBound` (0.1 s) also applies while `pitchNeedDn` is up.

## From the 16:29 flight (2.9.0)
Three released-roll push-overs from 65°: 179 kt / 46 deg/s of yaw → 7.9°; 229 kt / 62 → 9.3°; 209 kt / 78 → **20.6°**
(75.0 s), with the "pitch hitch instead of a smooth slower pitch rate" at the end of the same push.

What 2.9.0's priority did and did not do (75.0 s):
- 65 → 50°: as before — stabs off pitch (`stabPitchFrac` 0), TVC-only pitch, the right stab parked at −30 on the roll stop.
- 50 → 35°: `pitchNeedDn` 1, **pitch weight 8 / roll 0.5–0.8** — the weights took. The allocator sent the right stab −30 → +35
  and **left the left stab at −6**. In command space the right stab's 65° of travel covered the whole pitch increment, so
  the left was left on the roll. The servo took 0.9 s over those 65° (75.9 → 76.7 s); the nose went 40 → 20° AoA meanwhile
  (q −13 → −27 against a command of −10 → +7). The capacity was there (41° of left-stab nose-up unused); the allocator
  has no notion of servo rate, so it banked on travel that was still 0.9 s away.
- 20 → 15°: the recovery overshot nose-up (q −27 → −1.5 as the right stab arrived at +35 and the nozzles at −20), the cap
  let go at 15° with 20 deg/s of yaw left, and the same stab swung **+35 → −30** for the push — another 0.9 s. That is the
  hitch: q ≈ −2 for 0.3 s at 17–18° AoA, then −47.
- 20 → 0°: 2 → 20.6° of sideslip from the 40 deg/s of yaw still on at 20° AoA (shed 15–30 deg/s² at 25–35°, 50–60 below).

## Change
`relRb = max(relStick, pitchNeedDn) × stabPitchFrac`: while the capped sweep's pitch hold has the stabs, each allocation may
ask a stabilator only for what its servo reaches in `ReleasedStabRateBound` 0.1 s (≈ 7.5°) from where it is — the 1.8.14
device that already does this with the stick released, now also under the hold. The servos still move at full rate; what
changes is that the allocator cannot bank one stab's far travel, so the pitch increment goes to both stabs and the nozzles
at once (twice the symmetric rate, no stop-to-stop swing, and a smaller back-swing when the hold lets go). Expected on the
75.0 s push: the hold arriving ~0.4 s sooner at 40–45° AoA and the hitch at 17° gone or much smaller; whether 78 deg/s of
rotation at 209 kt then fits the margin is still a flight question — the yaw shed at 25–35° AoA was 15–30 deg/s² against
40–50 deg/s to kill.

## Bench
Reversal suite, corkscrew entries, regression set, utilisation invariant: identical to 2.9.0 with the device off.

## Carried over
Everything in the 2.9.0 notes' last section.
