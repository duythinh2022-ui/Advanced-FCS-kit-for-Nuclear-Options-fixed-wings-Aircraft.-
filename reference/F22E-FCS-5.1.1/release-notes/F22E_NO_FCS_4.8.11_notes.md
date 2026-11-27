# F-22E Strike Raptor FCS 4.8.11 (Nuclear Option)

Base: 4.8.10. Bench only, not flown.

## Defaults (what you asked for)
- `NoTvcAoACapPositive` / `NoTvcThrustAoACapPositive` **55**, `NoTvcAoACapNegative` / `NoTvcThrustAoACapNegative` **50** (were 45/45).
- `TwoZoneAirframe` **on** by default.
- `FinBlanking` **on** by default. The real code defaults are min **0.5** of fin effectiveness, fading from **38** to **46 deg** AoA. My earlier 0.6 / 40-55 figures were wrong.
- New key `TwoZoneSlatAftGain` (5. Airframe), default **0.55** (was a fixed 0.4). This is the slat-coupled aft lift that wins back most of the roll that blanking costs (bench: rev roll at 45/50 deg no TVC goes 47.0/44.7 -> 51.0/48.2 deg/s). Respawn needed (SyncAirframe).
- These are code defaults. A key already in your cfg keeps its value.

## Corkscrew: what the log and the bench show
Log `F22E_FCS_20261126_021036.csv` (4.8.10, no TVC, +55/-50). The bench reproduces it (`cork.sh`: full aft + full roll, push at 4 s held to 12 s, then release to neutral or to full aft).

**Held push, roll dip at zero AoA (fixed).** As the AoA crosses zero the pace window compares the yaw it has against the yaw needed at the horizon AoA. A shortfall of about 3 deg/s, divided by sin(-6.6 deg), became a cut in the roll target from 52 to 13-22 deg/s in one tick. The roll loop then braked at -200 deg/s^2 and took the rudders away from the yaw it was waiting for. In the bench the roll went 54 -> 20 -> 55 deg/s across the crossing.

**Held push, slow pitch-over (physics, not changed).** At 220 km/h with no TVC, from 54 deg:
- The net nose-down authority is 25-33 deg/s^2. The stabs and the rudder toe are both on their nose-down stops.
- The pitch plan uses what exists.
- The coordinating yaw (pS sin AoA, 38 deg/s at the start) has to shed before zero AoA, at the 15-25 deg/s^2 the yaw has left. Reducing the roll sheds the same yaw, so taking roll away does not give the pitch more.
- I tried a roll/pitch share of the yaw (PushShare). It changed nothing measurable (pS did not follow, because the shed is the binding constraint), so it is not in this build.

**Release (pitch delayed, roll stop).**
- After a push to -50 deg the nose-up rate is held to about 54 deg/s of AoA rate by the AoA-rate stop. That rule says the rate must be stoppable within 15 deg, and nose-down authority at -50 deg is only 35 deg/s^2.
- The roll stop takes about 0.8 s. That is set by the yaw shedding r = pS sin AoA (-32 deg/s) to zero.
- Both of these are the airframe at that state.
- The share that was wrong: with the stick released, the lateral stab share dropped to zero. The pitch plan and the released-pitch priority then took the stabs from the roll stop (roll unmet -36..-71 deg/s^2 in the log).

## Changes
1. **PaceLeverage** (pace governor). How hard the window holds the roll is now weighted by how much the roll actually moves the yaw:
   - `sin^2 a / (sin^2 a + 0.0076)`: half at 5 deg, 94 % at 20 deg, 99 % at 45 deg.
   - It is the least-squares split of the yaw shortfall between sideslip rate and roll change.
   - No gate. It replaces a 1/sin a singularity.
2. **LatShareFromPlan** (pitch law). The lateral stab share follows the lateral *demand*: the larger of the stick and the roll plan's share of the remaining work on both plans (each plan's remaining change over its own acceleration). Sharing the stabs in that ratio brings both plans to their targets together. A roll stop with the stick released now keeps its share.

## Bench (two-zone, fin blanking on, slat aft 0.55)
Corkscrew, held full push + full roll, then release (4.8.10 -> 4.8.11):

| Case | Roll dip at zero AoA | Sideslip max | Release roll stop | Release sideslip |
|---|---|---|---|---|
| 180 km/h no TVC | 32.9 -> 16.2 | 8.5 -> 9.2 | 0.82 -> 0.88 s | 1.7 -> 2.0 |
| 220 km/h no TVC | 33.9 -> 14.9 | 8.3 -> 8.0 | 0.80 -> 0.82 s | 4.5 -> 1.6 |
| 260 km/h no TVC | 12.4 -> 14.7 | 5.4 -> 4.3 | 0.40 -> 0.40 s | 3.4 -> 3.5 |
| 300 km/h no TVC | 11.3 -> 11.6 | 3.9 -> 3.2 | 0.37 -> 0.37 s | 3.1 -> 3.1 |
| 220 km/h TVC | 34.0 -> 16.9 | 12.9 -> 13.2 | 0.90 -> 1.00 s | 2.5 -> 2.4 |
| 300 km/h TVC | 12.0 -> 6.2 | 3.8 -> 3.6 | 1.00 -> 0.42 s (q > 0 at 1.13 -> 0.43 s) | 2.3 -> 3.6 |

Release into full aft at 220 no TVC: q > 0 at 0.42 s, AoA past -10 at 1.12 s, both unchanged. Roll stop 0.80 s, roll unmet 20.7 -> 15-17.

Batteries (pitch/roll/yaw):
- No TVC (`bat.py`): within noise. Rev rate 30 deg 65.4 -> 67.9. Slow-roll slip at 36 deg 6.9 -> 7.8.
- TVC: roll rates the same. Rolling-pull release (prolls 45) sideslip 2.8 -> 5.4. Pull 45 sideslip 6.5 -> 4.8.
- Low/mid AoA (`lowbat.py`): same, except 450 km/h at 16 deg with TVC, sideslip 2.9 -> 4.5. Push dq unchanged everywhere.

Tried and dropped (they did not bind in the cases you flew):
- The AoA capture's braking integrated over the path to the limit.
- The g-capture's braking read at the limit AoA.
- The PushShare roll/pitch yaw share.
