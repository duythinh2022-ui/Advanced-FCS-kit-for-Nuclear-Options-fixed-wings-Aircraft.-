# Revoker FCS 0.7.0: the leaf reversal keeps the yaw nozzle, the push plan stops the roll, and a 6° stop margin

Base: 0.6.0. Minor bump: these are law changes.

**Status:**
- Compiled.
- Benched (full suite).
- **Not flown.**
- 0.6.0 is kept as `.dll.0.6.0.bak`.

## Tom (5 Nov, 00:56 / 01:10)
- "falling leaf reversal is still slow. The tvc nozzles stop the roll first, then it go to neutral, then yaw to the other direction."
- Reversals at low to mid AoA make 7–9° of sideslip: "reduce by 2 degs, even if sacrificing some roll rate".
- Decisions: stop the roll during the push transient; `RollStopSideslip` lowered on both jets.

## 1. Falling-leaf reversal (`RudderReach`)
**What the log shows:** 00:44 log, both leaf reversals (73.0 and 76.9 s, 69° AoA):
- The yaw nozzle brakes the old yaw at −15°.
- At the yaw crossing, the allocator sends the rudder −2 → −25° (its stop) and backs the nozzle off to about −2°, as if the rudder were already
  there. The rudder needs about 0.35 s for that travel.
- The rudder then flips −25 ↔ +25° every few ticks until the nozzle comes back for the new side.
- The nozzle gives up 11–13°, and the roll takes 0.9–1.1 s to its zero.

**The change:** the allocator's reach pass (F-22E 2.41/2.43, here only on the canards) now also covers the rudder, in flight. The other effectors
are placed against where the rudder will be in 0.1 s, not where it was sent, so the nozzle keeps braking.

**Bench (this change alone):**
- No case worse by more than 0.4°.
- roll-then-pull-rev 400: 7.6 → 4.6°.
- Sweep reversal worst: 9.5 → 8.5°.
- The leaf dither itself doesn't happen on the bench (different fin sidewash), so this is **flight-validated**.

## 2. The push plan stops the roll (`PushPlanStopsRoll`)
Same as Ifrit 0.11.0: under a planned push or pull-through, the roll bleeds to zero with the stick held too. It comes back below 10° AoA.

**Damage suite, clean held push-overs:**

| | 0.6.0 | 0.7.0 |
|---|---|---|
| time to 10° AoA | 1.53–1.55 s | **1.38–1.40 s** |
| sideslip | 5.8–6.2° | **2.3–2.6°** |

Released pushes are unchanged.

## 3. `RollStopSideslip` 8 → 6
**What the flight shows:** 00:44 log, 9 reversals at 17–21° AoA with full aft stick. All peaked at 7.3–9.2° with the 8° margin used up.

**The change:**
- The default is now 6.
- A config file still holding exactly 8 is moved to 6 once (`GovernorRevision` 2). A value you set by hand is kept.
- Expect about 2° less, at some roll rate.
- On the bench the margin rarely binds, so the suite barely moves.

## Bench, full suite (0.6.0 → 0.7.0)
- **Sweep:** stop worst / mean 5.3 / 3.02 → 5.4 / 3.01°; reversal 9.5 / 3.94 → 8.5 / 3.78°; mean roll 113 (same).
- **Regression suite:**
  - better: roll-then-pull-rev 400 7.6 → 4.6°, push-then-pullthrough 350 7.0 → 5.8°, roll-then-push −55 4.2 → 3.8°;
  - worse: pullthrough with the roll released at zero, 4.9 → 5.5° (the roll now stops through the pull).
- **Damage suite:** only the clean held pushes changed (above).

## Install
- `plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` and `meta.json`.
- Release: `C:\Games\NO mods\Revoker-FCS-0.7.0-release`.
