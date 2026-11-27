# F-22E Strike Raptor FCS 4.8.6 (26 Nov 2026)

Ask: Tom 26 Nov 00:43, after 4.8.5 took the stab back to 4.8.2's: "i told u that i want 25 with the changed stability margin, similar trim cost to 4.8.2 and same pitch/roll/yaw pwr with better pitch reversal response". Bench only; not flown.

## Change
1. `StabSplitCost` (4. Gains, 0.05, all airframes): the least the allocator charges for splitting the two stabs against each other. Before, that cost was 0.0002-0.01 by speed and pitch use. One line in the cost schedule (`FcsController`, stab `antiCost`); no gate, no timer.
2. Two-zone stab back to the 25 deg whole-stab stall: `TwoZoneStabStallAoA` 33, `TwoZoneStabCLScale` 1.09, `TwoZoneStabUpwash` 0.24 (defaults and Tom's Dev cfg).

Kept from 4.8.3-4.8.5: hard no-TVC cap, pitch plan share 0.95, branch switch off on two-zone, low-AoA unstable couple.

## Why the cost fixes the stuck roll trim
Where a trimmed stab sits on its outboard lift peak, moving it trailing-edge down hardly changes pitch (the two zones work against each other), so the pitch cost is flat along the split direction. With almost no cost on the split itself nothing held the pair together: one stab drifted off, the flaperons and rudders cancelled its roll, and any model error wound it further (00:30 log: mean split 9.2 deg at the 35 deg limit, roll-disturbance estimate 0 -> 17 deg/s^2). A cost of 0.05 is small against any real roll demand and large against a flat cost.

## Bench
No-roll full-aft hold, 14 s, mean / max stab split (deg), 25 deg stab:

| Case | limit 30 | 35 | 40 | 45 |
|---|---|---|---|---|
| 4.8.4, no TVC, plant = model | 0.3 / 0.9 | 6.4 / 7.6 | 2.2 / 6.2 | 0.6 / 7.1 |
| 4.8.4, no TVC, plant off-model | 0.2 / 0.4 | 10.4 / 14.3 | 2.0 / 7.6 | 0.7 / 8.4 |
| 4.8.6, no TVC, plant = model | 0.0 / 0.0 | 0.0 / 0.0 | 0.0 / 0.0 | 0.0 / 0.0 |
| 4.8.6, no TVC, plant off-model | 0.0 / 0.0 | 0.0 / 0.0 | 0.0 / 0.0 | 0.0 / 0.0 |
| 4.8.6, no TVC, 250 km/h, plant off-model | 0.0 / 0.0 | 0.0 / 0.0 | 0.0 / 0.0 | 0.0 / 0.0 |
| 4.8.6, TVC, plant off-model | 0.0 / 0.0 | 0.0 / 0.0 | 0.0 / 0.0 | 0.0 / 0.0 |

TVC at the 55 / 65 deg limits: 0.1 / 0.7 and 0.2 / 1.1. Flaperon split in all of them 0.2-0.8 deg mean.

Battery, two-zone, 450 km/h, limits 30/36/45/50:

| | 4.8.2 no TVC | 4.8.6 no TVC | 4.8.2 TVC | 4.8.6 TVC |
|---|---|---|---|---|
| Reversal roll, deg/s | 64.9/55.3/54.7/51.5 | 68.6/59.8/54.6/52.0 | 78.2/69.3/57.0/51.5 | 77.8/72.4/56.4/51.4 |
| Limit overshoot, deg | 0.6/0.5/0.4/0.7 | 0.6/0.7/0.5/0.4 | 1.0/0.9/0.8/0.8 | 0.9/0.9/0.9/0.8 |
| Reversal sideslip, deg | 2.7/3.4/4.2/3.6 | 3.7/2.7/4.6/3.5 | 2.7/3.5/4.2/2.9 | 5.7/3.8/6.2/5.0 |
| Push from 36/45, deg/s in 1 s | -24.8/-20.7 | -34.6/-24.2 | -46.6/-40.8 | -47.2/-42.6 |
| Loaded roll from 36/45, deg/s | 148.2/94.3 | 141.2/112.8 | 150.5/117.3 | 143.1/114.0 |
| Release from rolling 36 deg hold, sideslip | 8.4 | 7.0 | 5.3 | 4.7 |

Worse than 4.8.2: reversal sideslip with TVC (+1.5 to +3 deg at three of four limits), loaded roll from 36 deg (-5 %).
X2e with the cost: reversal roll 48.4/36.8/30.2/27.9 (48.2/36.8/30.5/28.1 without), TVC 72.1/60.0/45.0/39.1 (72.1/58.6/45.2/39.1); overshoot and pushes within scatter.
(Battery rows for 4.8.6 were run as the same cost through the existing `HiAoAStabRollCost` test knob before the build; holds and the release case on the built defaults.)

## Not met: "similar trim cost to 4.8.2" at low AoA
Low-AoA stab trim is still about +7 deg trailing-edge up (4.8.2: +3 to +6 on the bench), because of `TwoZoneLowAoAUnstable`. Stabs neutral, the pitching moment at 28 deg is -24 deg/s^2 (400 km/h). Unstable below 20 and neutral 20-28 means the moment at 0 deg is below the moment at 28 by the area under the instability: about 17 deg/s^2 for -0.1 m of margin. So:
- unstable + unchanged past 28 -> about -41 at low AoA -> the +7 deg trim (what is built);
- unstable + 4.8.2 trim at low AoA (-2 at 2 deg) -> the moment at 28 deg has to be about +15 instead of -24, i.e. the whole curve from 28 deg up sits about 39 deg/s^2 less nose-down: stab trim at 30-50 deg near neutral instead of +11 to +22, more nose-up power, about 39 deg/s^2 less nose-down power from 28 deg up at 400 km/h (not built, not benched);
- 4.8.2 trim everywhere + unchanged past 28 -> no instability (`TwoZoneLowAoAUnstable` = false).

## Open
- Parked-stab solver work (4.8.4 notes) is unchanged by this.
- The bench's log calibration is X2e's; the split only reproduces with the plant at or near the game's aero as written.
