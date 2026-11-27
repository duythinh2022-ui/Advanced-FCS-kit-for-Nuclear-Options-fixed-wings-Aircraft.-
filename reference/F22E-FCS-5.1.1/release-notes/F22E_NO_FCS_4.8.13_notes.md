# F-22E Strike Raptor FCS 4.8.13 (Nuclear Option)

Base: 4.8.12 (20 deg whole-stab stall). Bench only, not flown. Asked for: fix the sideslip 4.8.12 added, through the airframe or the laws.

## Causes found
1. **No TVC, 15-20 deg AoA: the stabs' nose-down travel was clipped.**
   - The two-zone stab clamp (`TwoZoneStabPeakClamp`, 4.8.2) keeps each stab's outboard zone 3 deg short of its lift peak below 20 deg AoA.
   - With the 20 deg stab that left 2-4 deg of the 15 deg of nose-down travel the stab really has. Past the outboard peak the inboard zone still adds nose-down.
   - In a fast roll at those AoAs the roll's inertial pitch-up then won. Bench case: pull released from 35 deg plus full roll. The AoA hung at 16-17 deg with 72 deg/s^2 of pitch unmet.
   - The clamp exists so the nozzles carry the pitch instead of a stab parked past its peak (4.8.1). With no pitch nozzles nothing else can.
2. **30-40 deg AoA: the stabs trimmed on their lift peak.**
   - With the 20 deg stall the band where the trimmed stab sits on its lift peak moved from 38-40 deg (4.8.11) to 32-38 deg.
   - On the peak a split barely rolls: split roll power at 36 deg was 2.3 against 4.0 deg/s^2 per deg in 4.8.11. The allocator took the roll on the rudders.
   - The rudders were then at the stop (+-30) when a roll stop needed yaw. That is the reversal and release sideslip.

## Changes
1. **Clamp margin without pitch nozzles** (law): `TzOutClampMarginNoTvc` +8 deg past the outboard peak when the nozzles are off. With the nozzles on it is still -3 (4.8.2).
2. **Body couple, 32-40 deg** (airframe): a nose-down band (6/11/15/20/11 deg/s^2 at 32/34/36/38/40) in the fore/aft zero-net-lift tables.
   - It trims the stabs trailing-edge up off the peak: 32 deg +14.0, 34 +16.0, 36 +17.9, 38 +18.1 (4.8.12: 12.8/13.3/13.3/12.8).
   - That puts the split roll power back to 4.8.11's: 4.24/4.16/4.01/3.12 vs 4.16/4.08/3.97/3.27.
   - Trim below 30 deg is unchanged from 4.8.12.
3. Tried and dropped:
   - A nozzle-cover price on symmetric stab travel: no gain.
   - An off-peak symmetric target for the stabs with TVC: TVC reversal sideslip 2.6/3.4 -> 7.9/6.4.

## Bench: sideslip (deg) and roll (deg/s), 4.8.11 -> 4.8.12 -> 4.8.13

**No TVC**
| Case | 4.8.11 | 4.8.12 | 4.8.13 |
|---|---|---|---|
| Rev slip at 30/36/45/50 | 1.9/2.6/4.0/4.5 | 3.2/3.2/4.2/4.1 | 2.1/2.6/4.4/4.1 |
| Rev roll at 30/36/45/50 | 67.9/60.8/57.8/53.0 | 66.2/61.5/57.7/52.7 | 67.0/66.5/57.8/52.6 |
| Slow (idle) roll, slip at 36/45 | 7.8/5.6 | 7.6/5.1 | 4.8/4.6 |
| Pull, slip at 36/45 | 4.7/4.4 | 5.5/6.0 | 3.7/4.4 |
| Pull, release, roll at 36 | 139, slip 3.2 | 138, 2.1 | 150, 3.0 |
| Same at 45 | 111, 6.7 | 104, 7.0 | 111, 7.2 |
| Idle pull-release-roll (prolls), slip at 36/45 | 10.2/5.7 | 13.0/7.0 | 10.6/5.9 |
| Half-stick, slip at 36/45 | 8.8/2.8 | 7.9/3.5 | 9.3/2.9 (roll 44 -> 48) |
| Push in 1 s from 36/45 | -33.0/-30.4 | -33.6/-31.1 | -37.2/-34.6 |
| Corkscrew 300 km/h, held-push slip | 3.2 | 7.5 | 5.0 (pitch-over 1.37 s vs 1.62) |
| Corkscrew 220 km/h, held-push slip | 8.0 | 7.4 | 7.5 |

**TVC**
| Case | 4.8.11 | 4.8.12 | 4.8.13 |
|---|---|---|---|
| Rev slip at 30/36/45/50 | 2.0/1.8/3.3/3.1 | 5.3/4.4/4.2/3.4 | 2.6/3.4/4.1/3.4 |
| Slow (idle) roll, slip at 36 | 4.0 | 4.2 | 5.5 (roll 42.4 -> 45.5) |
| Pull, slip at 45 | 7.6 | 4.2 | 5.4 |
| prolls, slip at 36/45 | 3.6/7.3 | 1.9/5.5 | 4.6/8.3 |
| Corkscrew 220 km/h, held-push slip | 13.2 (4.8.11 run) | - | 12.9 |

**Low/mid-AoA battery**: within 1 deg of sideslip and a few deg/s of roll rate, both ways. Pushes are equal or faster.

**Stuck-split hold test** (320 km/h, 14 s at 28/32/35/38 deg, TVC on and off): split 0.0 deg.
- Stab trim without TVC: 13.0/16.9/21.0/23.1.
- With TVC: 4.5/0.3/0.2/0.1 (the nozzles carry the pitch).

## What is still worse than 4.8.11
- TVC reversal sideslip at 36 deg: 3.4 vs 1.8.
- TVC idle rolls: about 1 deg more sideslip, with about 3 deg/s more roll rate.
- No-TVC corkscrew push at 300 km/h: 5.0 vs 3.2 deg. The pitch-over is 0.25 s faster.

## Trim cost
- No TVC: 32-38 deg is 1-5 deg more trailing-edge up than 4.8.12 (about +4 to +6 vs 4.8.11).
- With TVC the nozzles take it.
- Below 30 deg: as 4.8.12.

## Going back
- To 4.8.12 or 4.8.11: reinstall the `.bak` DLL.
- To 4.8.11 also set `TwoZoneStabStallAoA` 33 and `TwoZoneStabCLScale` 1.09 in the dev cfg.
