# F-22E FCS 2.33.0 — the released-follow yaw brake reads the low-passed capacity

Base: 2.32.0. **One law change. Compiled against the game's assemblies, bench-checked, not flown.** 2.32.0 kept as `.dll.2.32.0.bak`.
No player or Dev key added; CSV unchanged.

## The problem (Tom, 19:05: "it still oscillate like a few times then settles" — 19:02 log, 2.32.0)
Where `ReleaseFollowYaw` engages legitimately (70.5 s: pull easing 0.44 → 0.27, AoA 28 → 23°), its yaw brake flipped tick by tick:
rCmd −9 / +17 / −9 / +13, rudders 35 / −10. The brake asks for `capEst.capZeP/N`, a one-tick read built from the allocator's previous
solution (rudders commanded to 35 → 33 deg/s²; at −10 → 89): command → allocator → capacity → command with one tick of delay and loop
gain ~1. Upstream of INDI, which tracked the flipping command. In every 3 Nov flight 17–31 % of follow-engaged ticks flipped (2.31.0: 18 %).

## The change (`ReleaseBrakeCapLp` = true; false = 2.32.0 byte for byte)
The brake reads `capTrZeP/N` — the same capacity through the 3-tick low-pass (`TradeCapTau` 0.05 s) the trade has read since 2.21.0
("a one-tick reading is not a capacity"). Nothing else in the follow changed.

## Bench
| check | result |
|---|---|
| 19:02 log, pinned replays 69.8 / 75.3 / 97.8 s | rCmd flips bench 15 / 29 / 22 (game 19 / 22 / 36) → 0 / 3 / 0; mean |ΔrCmd| per tick 2.5 / 3.0 / 1.7 → 0.7 / 0.9 / 0.7; sideslip peak +1.0 / +0.3 / −0.4° |
| 22 push-overs + 11 snap releases (free replays) | 16 identical; changed ones: flips 238 → 5, sideslip peak median 6.7 → 7.3° |
| worse | 15:25 119.9 s released push from 65°: 5.7 → 9.2° — the flipping read braked at its high value half the time (the brake acts only when it asks for more than the law); the low-passed read is the mean, a softer brake, the yaw stops later and the sideslip swings to −7.8 instead of −4.8 |
| better | 14:45 42.7 s released push: 9.7 → 7.4° |
| pitch | two pushes pitch down faster on the bench (15:25 33.95 s, 14:45 54.6 s) — not judgeable on this bench (open pitch item) |
| full suite vs 2.32.0 | pullthrough released at zero 350: 4.4 → 4.2°; one-fin released push 560: 2.0 → 1.6°, 600: 5.0 → 5.9°; everything else identical. Switch off: all 119 case files identical to 2.32.0 |

## Not changed
The 2.20.0 no-fin stop (`PaceStopNoFin`) reads the same one-tick capacity — left as is (one change per build).

## What to fly
The 19:02 cases: easing a pull at 20–30° with the lateral stick released after a roll — the yaw should brake without the 30 Hz rudder
flip. Released push-overs from 65° — watch for the yaw stopping a little later than on 2.32.0 (the bench's one worse case).
