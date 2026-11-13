# F-22E FCS 3.1.7: push-over pitch curve hand-offs (no notches, no command spikes, no freeze)

Base: 3.1.6. Patch bump, control-law only. No config changes: no new player or Dev keys, no AirframeRevision migration. The new switches are code statics, bench-only.

**Status:**
- Compiled against the game's assemblies.
- Benched (below).
- **Not flown.**

## Tom (6–7 Nov)
- 23:44: "instant pitch response from stick input, an actual smooth pitch rate curve, no spike no hitch no sudden slow down, no chatter, no hesitation".
- 00:27: "smooth pitch curve, instant response, zero hesitation, zero chatter" (bench only).
- 00:46 (00:23 log): "at the beginning of every push over, the TVC would hesitate or chatter, then it went smooth for a sec, then it suddenly spikes or dips. Towards the end, the push over was slow, like only a few deg/s for a few moments."
- 00:51: "if it is real excess yaw, then just go, I'd rather have the pitch down than basically almost stay frozen for a few sec for that yaw to catch up, that is what the floor meant for, let the yaw/roll do its best during that pitch down, some more sideslip is ok".

## What was wrong (all hand-offs between the push plan and the sweep caps)
1. **Plan finishes → governor floor.** At 10° AoA the plan ramped out and handed the push to the governor's toward-zero cap, which came back at its 6°/s SweepFloor. The command went −50 → −23 for a tick, then back.
2. **Wrong-side cap for a tick at zero AoA.** The pitch law read the caps sized on the last lateral pass for the other AoA side. A pull through zero under a 20°/s roll went 39 → 32 → 39 in one tick.
3. **Pace with nothing to shed.** The governor's pace closed on a_min at 40°/s² even when a_min was 0 (no yaw over the allowance), then let go at the crossing. A pull from −48° with a 10–20°/s roll ran q 40 → 25 → 40.
4. **Grow cap past zero switched in whole at a 5°/s roll.** A push through zero with the roll dying clipped the command −53 → −24 for a tick.
5. **The governor took back rate the push already had.** Once the nose was going, a roll building during the push brought the toward-zero cap in (−45 → −23) and the nose went 45 → 23 → 45. This is the "spike or dip" and the "frozen" part.
6. **Plan bottom.** The plan held its rate, then ramped to 60 at 60°/s² below 10° and was cut at the crossing (−40 → −60 in 0.3 s).

## The changes (FcsController statics, all on)
| switch | what |
|---|---|
| `PushPlanDoneOpen` | a plan that finished at the bottom keeps the toward-zero side open until the AoA changes side or the stick lets go |
| `SweepSideKept` | the caps are read for the AoA side they were sized for |
| `PushGovPaceYaw` (`PushGovPaceR` 3) | the governor's pace fades in with the excess yaw r_ex (open at 0, whole at 3°/s); fins healthy only |
| `PushGovFadeGrow` (`PushGovFarLo/Hi` 20/40) | the far-side grow cap (past zero AoA under a push) fades in from a velocity roll of 20 to 40°/s |
| `PushNoRetreat` | **Tom 00:51.** In a held push (or a pull from negative AoA toward zero), the sweep caps may limit how fast the rate builds. They never take back the rate the trajectory has reached, until the stick lets go. Toward-zero side only; fins healthy only. |
| `SweepZeroBlend` (`SweepZeroDecel` 60) | push-overs only: with a big roll still held, the toward-zero sweep eases into the far side's rate before zero instead of hitting it as a wall. The 23°/s floor carries on past zero. |
| `PushPlanThrough` (`PushPlanReleaseSoft` 30) | the plan's ramp-out to 60 goes on through zero at 30°/s² (was 60, and cut at the crossing). The caps see the ramp-out. |

Also two bench switches, unchanged at the 3.1.6 values:
- `PitchTrajAccK` 0.7: the trajectory's share of the pitch acceleration. At 0.85 or 1.0 it made new dips and overshoots on the low set.
- `PitchTrajJerkT` 0.15. At 0.08 there were more nozzle reversals.

## Bench
### The 00:23 log's 9 push-overs (replayed, 3.1.6 logic vs 3.1.7, over the held push)
| push | nozzle reversals | command spikes | rate wobble | peak sideslip |
|---|---|---|---|---|
| 18.57 | 9 → **3** | 1 → 0 | 0 → 0 | 3.9 → 6.3 |
| 27.77 (plan) | 3 → 3 | 0 → 0 | 2 → 1 | 6.1 → 6.1 |
| 39.42 (plan) | 4 → 3 | 0 → 0 | 2 → 2 | 8.9 → 8.9 |
| 66.72 (43 kt) | 2 → 3 | 0 → 0 | 0 → 0 | 22.4 → 24.5 |
| 98.29 (49 kt) | 5 → **1** | 1 → 0 | 2 → **0** | 2.1 → 2.1 |
| 50.07, 55.95, 58.23, 78.79 | identical | | | |

- **66.72 s:** with 3.1.6 logic the rate went −45 → −22 and slowed again near 15°. Now it goes −45 → −30 in one smooth descent. The sideslip is already 22° in the game log (3.1.6).
- **98.29 s:** −60 held to the end. In 3.1.6 it went −60 → −23 → −50 → −23 → −58.

### 87 planned push-overs (5–6 Nov logs)
- Peak sideslip: never worse by more than 1°, better in 6. The worst is 28.1 → 24.4.
- Nozzle reversals: 280 → 235 in total, worst push 29 → 13.
- Command spikes: 14 → 4. Rate wobble: 24 → 22.

### Low set (48 cases, sub-100 kt)
- Pull-from-negative under a roll: the q 40 → 25 → 40 notch is gone (dip 10.7 → 1.1, command retreat 17 → 0.2).
- Command spikes: 5 → 1. Wobble: 8 → 1.
- Peak sideslip: −4.3 to +2.9° per case. The largest is 15.9 → 17.9, in push_0.6_4.0_0.5 at −46° AoA 3 s into a held full push (after the push-over, not in it).

### Suite
- Reg peak sideslip: the worst is 10.0 (unchanged).
- Two side effects found and fixed during the work:
  - `PushNoRetreat` on the far side: pull-then-pushover + full roll at 350 km/h reached 26°.
  - `PushPlanThrough` letting the plan own the pitch past zero.
- Push+roll 200: pitch command retreat 15.4 → 2.1.
- dmg: the no-fin and one-fin push-overs are within 0.2°. The roll rev 890 fins + R flaperon case departs in both.

## Install
- **Main:** `plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` 3.1.7 + `meta.json` 3.1.7. 3.1.6 is kept as `.dll.3.1.6.bak`.
- **Dev:** `plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 3.1.7.
- **Release:** `C:\Games\NO mods\F22E-FCS-3.1.7-release`.
