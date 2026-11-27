# F-22E Strike Raptor FCS 4.8.9 (26 Nov 2026)

Ask: Tom 26 Nov 01:38 / 01:46: X2e trim up to mid AoA, high AoA as 4.8.2; keep pitch / roll / yaw, the 25 deg whole-stab stall and the general behaviour. Bench only; not flown.

## Change
`TwoZoneLowAoAUnstable` (on by default) refitted: the zero-net-lift fore / aft body lift couple (`TwoZoneForeOffset` / `TwoZoneAftOffset`, `BodyLift.Offset`). Nothing else changed (stab 33 / 1.09 / 0.24, SlatAftGain 0.4, laws as 4.8.8).

Stabs-neutral pitch acceleration (log-calibrated bench, ~400 km/h, deg/s^2):
| AoA | 0 | 4 | 8 | 12 | 16 | 20 | 24 | 28 | 32 | 36 |
|---|---|---|---|---|---|---|---|---|---|---|
| 4.8.8 | -42.6 | -40.3 | -37.4 | -35.0 | -32.5 | -30.2 | -30.2 | -30.4 | -28.4 | -26.5 |
| 4.8.9 | -0.1 | +1.7 | +4.0 | +6.0 | +5.8 | -4.6 | -17.5 | -30.0 | -28.4 | -26.5 |
| X2e | 0.0 | +2.0 | +5.3 | +7.6 | +8.9 | +8.6 | +5.5 | +2.2 | +0.4 | +0.6 |

Static margin: unstable 2-14 deg (neutral point 0.06-0.12 m ahead of the CG), stable 16-28 (0.5-0.8 m), unchanged from 28 deg; nothing at negative AoA.

Trimmed stab (bench, deg TE-up):
| | 5 deg | 12 deg | 15 deg | 22 deg | limits 30/36/45/50 |
|---|---|---|---|---|---|
| 4.8.8 | +9.4 | +10.7 | +11.2 | +11.6 | +10.8/+13.0/+17.5/+23.0 |
| 4.8.9 | +1.0 | +2.4 | +2.8 | +7.3 | +10.8/+12.8/+17.4/+22.9 |
| X2e | +1.0 | +2.0 | +2.4 | +2.9 | +1.9/+1.9/+3.0/+7.8 |
| 4.8.2 (two-zone) | | | | | +9.2/+11.1/+16.0/+20.1 |

## Why not X2e trim at high AoA too
Tried first (couple carried to 40 deg, stab trim about 0 at the 30 / 36 limits): slow loaded roll with TVC 57 -> 35 deg/s, sideslip at 45 deg 3 -> 7-9 deg. The two-zone stab's outboard half sees about 1.24 x body AoA; at neutral at 36 deg that is about 45 deg local, far past its 33 deg peak, so a split rolls the jet little. TE-up trim keeps it near its peak, where the split has the most roll. The TE-up trim at high AoA is what this tail's roll costs; X2e's stab is in downwash and has no such need.

## Bench, two-zone, 450 km/h, limits 30/36/45/50
| | 4.8.2 | 4.8.8 | 4.8.9 |
|---|---|---|---|
| Reversal roll, no TVC | 64.9/55.3/54.7/51.5 | 63.1/57.9/55.3/51.9 | 63.2/57.3/55.0/51.5 |
| Reversal roll, TVC | 78.2/69.3/57.0/51.5 | 79.0/72.4/56.2/51.3 | 78.4/70.3/56.1/51.1 |
| Reversal sideslip, no TVC | 2.7/3.4/4.2/3.6 | 3.0/1.5/2.6/2.3 | 1.6/1.8/2.5/2.1 |
| Reversal sideslip, TVC | 2.7/3.5/4.2/2.9 | 2.4/2.0/2.7/2.1 | 4.8/1.9/2.5/2.2 |
| Push from 36/45 in 1 s, no TVC | -24.8/-20.7 | -34.0/-25.7 | -29.6/-25.0 |
| Push from 36/45 in 1 s, TVC | -46.6/-40.8 | -47.3/-43.6 | -44.2/-42.7 |
| Loaded roll from 36/45, no TVC | 148.2/94.3 | 143.0/113.3 | 137.6/100.0 |
| Loaded roll from 36/45, TVC | 150.5/117.3 | 151.2/122.2 | 150.8/120.4 |
| Slow loaded roll 36/45 (320 km/h idle), sideslip, no TVC | 8.4/6.5 | 6.8/3.0 | 11.1/4.7 |
| Rolling pull sideslip 36/45, no TVC | 10.0/9.0 | 5.6/3.8 | 4.4/4.3 |

Low-AoA battery against 4.8.2: rolls within 3 % except 450 km/h at about 18 deg AoA: 120.1 -> 106.7 deg/s without TVC, 133.5 -> 123.2 with (X2e 116.1). Pushes equal or stronger (300 km/h from 40: -32.6 -> -37.6; 450 from 45: -46.3 -> -50.5). Release grid (18 cases): worst sideslip mean 4.9, max 7.0 (4.8.8: 4.7 / 6.6). Idle stab split around a roll: 0.0-0.1 mean.

## Worse
- Roll at mid AoA: 450 km/h ~18 deg -11 % (no TVC) / -8 % (TVC) against 4.8.2; loaded roll from 36 deg without TVC 148 -> 138 (-7 %); from 45 deg 113 -> 100 against 4.8.8 (4.8.2: 94).
- Sideslip: slow loaded roll at 36 deg without TVC 11.1 (4.8.2 8.4); TVC reversal at the 30 deg limit 4.8 (4.8.2 2.7).
- Cause: with the stab trimmed near neutral at mid AoA, the outboard-zone peak clamp (`TwoZoneStabPeakClamp`, outboard zone kept 3 deg short of its peak below 20-30 deg AoA) leaves less trailing-edge-down room for the split: at 15 deg AoA about 14 deg instead of 23.
- A stronger slat-coupled aft lift (SlatAftGain 0.55 / 0.7) won back the mid-AoA roll and added pitch, but raised sideslip at 45-50 deg (to 4.3-4.9 without TVC); not used.
