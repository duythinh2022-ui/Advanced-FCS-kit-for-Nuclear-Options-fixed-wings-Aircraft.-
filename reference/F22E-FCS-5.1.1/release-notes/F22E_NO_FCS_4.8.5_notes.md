# F-22E Strike Raptor FCS 4.8.5 (26 Nov 2026)

Ask: Tom 26 Nov 00:33, after flying 4.8.4 (log `F22E_FCS_20261126_003012.csv`): "the jet now stuck in roll trim frequently, then the flaperons have to correct for that. Also why the stabs make a significant trim even at 3 aoa?" Bench only; not flown.

## Change
Two-zone stab defaults back to the 4.8.2 stab: `TwoZoneStabStallAoA` 33 -> 36, `TwoZoneStabCLScale` 1.09 -> 1, `TwoZoneStabUpwash` 0.24 -> 0.2. Tom's Dev cfg was edited to the same three values. Everything else from 4.8.3 / 4.8.4 stays (hard no-TVC cap, pitch plan share 0.95, branch switch off on two-zone, low-AoA unstable couple).

This undoes the "stabs stall at 25 deg whole-stab average" request of 23:38: that request is what caused the stuck roll trim.

## Cause of the stuck roll trim
- 00:30 log (4.8.4, DisableTVC, limit 35, full aft held, no roll or yaw stick, roll rate 0): at 33-37 deg AoA the stabs were split by more than 4 deg in 83 % of rows, mean split 9.2 deg (4.8.2 flight at 23:10: 62 %, 4.1 deg). Typical: left +14, right -9.4 (on its limit), inboard flaperons +-9 and outboard +-6 against each other, rudders -10 / +9, and the roll-disturbance estimate climbing 0 -> 17 deg/s^2 over 10 s.
- The cause is the 4.8.3 stab change, not the 4.8.4 branch-switch removal (the branch switch never fired in this log either way: `stabBranch` 0 in every row).
- Mechanism: the outboard zone of a two-zone stab sees (1 + upwash) x body AoA less the stab angle. With 33 / 0.24 and the trim angle the game needs at 35 deg (+3...+9), that is 34-40 deg: on or just past the outboard lift peak (33). Trailing-edge-down from there the two zones work against each other, so stab angle hardly changes pitch and the cost is flat; one stab drifts into that region, its roll is cancelled by the flaperons and rudders, and any model error winds it further.
- Bench reproduction (14 s full-aft hold, no roll input, no TVC; plant = the game's aero as written, and a plant halfway to the X2e log fit). Mean / max stab split, deg:

| Limit | 4.8.4 stab, plant = model | 4.8.4 stab, plant off | 4.8.2 stab, plant = model | 4.8.2 stab, plant off |
|---|---|---|---|---|
| 30 | 0.3 / 0.9 | 0.2 / 0.4 | 0.1 / 0.1 | 0.2 / 0.2 |
| 35 | 6.4 / 7.6 | 10.4 / 14.3 | 0.1 / 0.2 | 0.1 / 0.5 |
| 40 | 2.2 / 6.2 | 2.0 / 7.6 | 6.0 / 8.2 | 5.6 / 8.4 |
| 45 | 0.6 / 7.1 | 0.7 / 8.4 | 4.3 / 8.0 | 4.3 / 8.8 |

- So 4.8.5 does not remove the effect: it moves it from the 35 deg limit back to 40-45 deg, where 4.8.2 had it (23:10 log, 45-56 deg: 48 % of quiet rows split by more than 4 deg). The log-calibrated bench (stab x0.72, CG 15.5 cm forward) never showed it at 35 deg because it trims 6-11 deg further trailing-edge up than the game does there; that calibration is X2e's and is off for the two-zone airframe.
- `RollFightReleased` = true (existing setting) cut the 35 deg split to 1.3 / 2.8 with plant = model but only to 6.4 / 8.9 with the plant off, and it was turned off in 2.3.2 for holding stuck sets with TVC on. Not used.

## Why the stabs trim at 3 deg AoA
That is `TwoZoneLowAoAUnstable` (4.8.3, asked 23:54: unstable at low AoA, neutral 20-28, unchanged past 28). Stabs neutral, this airframe pitches nose-down at 28 deg (-24 deg/s^2 at 400 km/h). "Unstable below 20" means the nose-down moment gets smaller as the AoA rises toward 20, so it has to be larger at low AoA than at 28: -41 deg/s^2 at 2 deg, -32 at 10 deg. The stabs hold that with about +7 deg trailing-edge up in level flight (00:30 log: +7.2 / +7.2 at -5...3 deg AoA; 4.8.2: +2.7...+6 on the bench). The three asks cannot all be met without it: no trim at low AoA and unstable to 20 deg would need a strongly stable band at 20-28 to get back down to the unchanged moment at 28. `TwoZoneLowAoAUnstable` = false gives the 4.8.2 low-AoA airframe.

With the 36 deg stab the couple (fitted for the 33 deg stab) gives: neutral point -0.15...-0.13 m at 2-18 deg, -0.09...-0.02 at 20-24, +0.09 / +0.17 at 26 / 28, unchanged from 30.

## Bench, 4.8.5 (two-zone, 450 km/h, limits 30/36/45/50)
| | 4.8.2 no TVC | 4.8.5 no TVC | 4.8.2 TVC | 4.8.5 TVC |
|---|---|---|---|---|
| Reversal roll, deg/s | 64.9/55.3/54.7/51.5 | 60.7/59.5/54.7/51.6 | 78.2/69.3/57.0/51.5 | 78.9/71.1/56.4/51.5 |
| Limit overshoot, deg | 0.6/0.5/0.4/0.7 | 0.6/0.5/0.4/0.7 | 1.0/0.9/0.8/0.8 | 0.9/0.8/0.9/0.9 |
| Reversal sideslip, deg | 2.7/3.4/4.2/3.6 | 2.9/5.5/4.7/4.9 | 2.7/3.5/4.2/2.9 | 2.5/3.8/3.5/3.0 |
| Push from 36/45, deg/s in 1 s | -24.8/-20.7 | -33.6/-23.3 | -46.6/-40.8 | -47.8/-43.6 |
| Loaded roll from 36/45, deg/s | 148.2/94.3 | 143.7/111.9 | 150.5/117.3 | 147.5/122.4 |
| Release from rolling 36 deg hold, sideslip | 8.4 | 7.5 | 5.3 | 5.9 |

Worse than 4.8.2: reversal roll at the 30 deg limit without TVC (60.7 vs 64.9) and reversal sideslip without TVC (up to +2.1 deg).

## Open
- The split of a stab trimmed on its outboard peak (now at 40-45 deg). It is the same solver weakness as the parked stabs (4.8.4 notes): a flat stretch of the cost that the one-effector-at-a-time search drifts along. A fix belongs in the allocator's cost / search, checked on the plant = model and plant-off holds above, which now reproduce it.
- A stab that stalls at 25 deg whole-stab average needs that fix first.
- The bench's log calibration should be refitted for the two-zone airframe.
