# F-22E FCS 2.7.1 — the measured governor, corrected from the first flight

Base: **2.7.0**. Patch: same law, three corrections from Tom's 09:55 / 09:57 logs. **Bench-only, not flown.** 2.7.0 kept as `.dll.2.7.0.bak`.

## What the logs showed
- **72.1 s, 413 kt, 15° AoA, reversal from +244 deg/s:** the roll braked 244 → 0 in 0.6 s at the full 720 deg/s² (rule 5 held), then **sat at 0–4 deg/s for 0.3 s** before building the other way; the new roll reached −200 only 1.5 s after the stick. The rudders were at −14° of 35°, `unmetZ` 0 the whole time: the yaw axis was idling.
- **64.8 s, 369 kt, 16°, from −205:** after stopping, the target went back *toward the old side* (−32 → −58) for 0.5 s — "keeps rolling left for a moment, then reverses".
- Entries (71.6–72.0 s): the roll built to 244 with the rudders at 8–20° and sideslip 0.2–0.4°. The yaw keeps up easily in the game where the bench's does not.

**Cause (mine):** 2.7.0 let the target lead the yaw only by what the yaw had *demonstrated*; the yaw loop's demand is proportional to that lead; so the yaw was never asked for more than it had shown — a self-fulfilling estimate. The laggard rule then pinned the target to the idle yaw.

## The corrections
1. **Open capacity.** While the allocator reports nothing unmet toward a side, the capacity there is what the axis is doing plus the committed increment arriving within the horizon **plus the authority still available beyond the commanded deflections** (every effector, from where the allocator sent it to its stop). It falls to what is being delivered as the allocator reports it cannot (`unmetZ/X` past 15 deg/s², fully at 55). With the effectors sent to their stops and still crossing (the 154 s / 339 kt case in the 01:08 log) the headroom is zero and the move is paced by what arrives.
2. **Pacing through the feed-forward, not a lead.** The target now *moves* toward the demand at the capacity (yaw capacity / sin AoA, roll capacity / cos AoA, as velocity-roll rates), so the loops' feed-forward asks for exactly the capacity with no error. A target moving back toward the axes is free.
3. **The lead** over the axis is the margin's (√(2a(margin − |β|)/cos AoA)) plus `PaceLeadT` 0.1 s of the capacity — the sideslip rate accepted while the roll waits — plus `PaceFloor` scaled by the margin left. (2.7.0's capacity × 0.15 s floor, and the capacity / KYaw floor tried in between, both drove the yaw through K × lead and cost 10–27 deg/s of sideslip rate to get the yaw to brake at all.)

## Bench (2× pessimistic on sideslip; its allocator tops out at ~75 deg/s² of yaw at 25° AoA / 400 km/h where your 01:08 log shows ~110)
| reversal | 2.6.11 | 2.7.0 | **2.7.1** |
|---|---|---|---|
| pull + roll 450 / 556 / 650 km/h, 11–13° | 0.23 / 0.23 / 0.28 s; 5.8 / 6.5 / 3.9° | 0.38 / 0.35 / 0.35 s; 8.0 / 4.4 / 4.1° | **0.37 / 0.32 / 0.37 s; 5.2 / 4.8 / 4.7°** |
| half pull 400 km/h, 29° (from 114 vs 63) | 0.25 s, 5.5° | 1.55 s, 5.9° | 1.33 s, 7.9° |
| half pull 556 km/h, 17° (from 185 vs 146) | 0.38 s, 5.8° | 1.45 s, 4.8° | **0.57 s, 8.3°** |
| half pull 700 km/h, 12° | 0.33 s, 4.1° | 0.43 s, 3.7° | 0.40 s, 4.2° |
| full aft 300 km/h, 62° | 1.53 s, 5.4° | 0.92 s, 4.1° | **1.00 s, 4.2°** |
| full aft 500 km/h, 49° | 0.40 s, 6.1° | 0.55 s, 6.3° | **0.60 s, 4.2°** |
| falling leaf 160 / 300 km/h | 2.28 / 1.78 s; 7.7 / 6.3° | 1.03 / 1.67 s; 9.5 / 5.8° | 3.02 / 1.88 s; 8.2 / 6.2° |

Utilisation invariant 1 FAIL / 9 (unchanged). Corkscrew entries within a few deg/s of 2.7.0. Regression set: push + roll / pull-through-zero family still 6–8° (the pitch-side lever, not built). The 160 km/h falling leaf is the one case clearly worse than 2.6.11 on the bench: the yaw there actually brakes at ~8 deg/s² against a capacity estimate of 15–23, so the pacing is honest but the estimate is optimistic — a corner with no authority on any axis.

## The trade, stated plainly (the half-pull cases)
A reversal from 185 deg/s at 17° needs the yaw to swing 2 × 185 × sin 17° = 108 deg/s. If the allocator can give 75 deg/s², that is 1.4 s, or the sideslip runs well past 4°. 2.6.11 made it "crisp" by trimming the steady roll to 146 beforehand (half the swing) and still made 5.8° bench. 2.7.1 does not trim (your rule 1), asks the yaw for everything it has, and paces the target to what it delivers. Where that is slower than you want, the choices are: more yaw (stabs as backup yaw — queued), a lower steady roll rate at moderate AoA (a tuning value, not a governor), or a wider margin (`RollStopSideslip`). Not a hidden pre-cut.

## "Why does it roll so fast"
Because nothing trims the steady roll any more: at 413 kt / 15° your log shows `pSTarget` = `pSmax` = 247, the airframe's own cap, with sideslip 0.2–0.4° — the yaw was keeping up, so by your spec the governor has no business cutting it. 2.6.11's rate cap was quietly taking ~40 % off that rate in advance of a reversal that might never come (that was the corkscrew dip). If you want less rate at moderate AoA for feel, that is `MaxRollRate` / the authority use, and I'll put a key on it if you say so.

## What to fly
Same checklist. The columns to read: `unmetZ` (should go negative/positive during every hard reversal — the yaw being asked for all it has), `capZN/capZP` against the yaw acceleration the jet then makes, `pSRaw − pSTarget` (the hold) against `beta`.
