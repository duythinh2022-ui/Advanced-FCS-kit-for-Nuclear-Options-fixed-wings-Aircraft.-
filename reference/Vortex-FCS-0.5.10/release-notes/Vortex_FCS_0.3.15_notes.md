# Vortex FCS 0.3.15: positive AoA limit +65 by default

17 Nov 2026. Tom, 20:17: "set default aoa limit to +65 migrate settings"; 20:35: "build .3.15 anyway" (0.4.0 is being built separately; this change still has to be carried into it). Benched, not flown.

## Change

- `AoALimitPositive` default 60 → 65°. `AoALimitNegative` stays 55.
- Migration `VortexConfigRevision` 11: a Dev file still holding exactly 60 (the 0.3.6 default) is moved to 65 once; any other value is kept. Tom's Dev file holds 60, so it moves.
- No law, allocator or native change; ABI 2013001. Section 6 (lateral governor) as 0.3.14 (the F-22E's).

**For 0.4.0:** the change is the default in the bind, the revision default 10 → 11, and the rev-11 block placed after the rev-10 block. If 0.4.0 already uses revision 11, use the next free one.

## Bench: 60 vs 65 (both with the 0.3.14 governor)

### Roll at the limit (full aft, then full roll; 250 / 300 / 360 km/h, both sides; mean of 6)

| | 60° | 65° |
|---|---|---|
| AoA held (300 km/h R) | 60.1° | 64.9° |
| Time to 10 deg/s VV roll | 0.95 s | 0.82 s |
| Time to 20 deg/s | 2.28 s | 1.91 s |
| VV roll at 1 s / 2 s | 10.9 / 20.2 deg/s | 14.1 / 22.3 deg/s |
| Max sideslip | 3.3° | 2.7° |

The right roll at 250 and 360 km/h is no faster (t20 3.73 → 3.63, 2.32 → 2.48 s); the gain is in the left rolls and 300 R. Sideslip at 250 R rises 3.1 → 4.9°.

### Held roll released (stop) and released with full push

| Case | Held VV roll (deg/s) | Time to stop (s) | Overrun (deg) | Max sideslip after (deg) |
|---|---|---|---|---|
| Stop, 250 km/h | 21.0 → 25.3 | 2.53 → 2.73 | 32 → **42** | 2.0 → 2.2 |
| Stop, 320 km/h | 24.3 → 27.6 | 2.45 → 2.41 | 35 → 38 | 2.4 → 2.0 |
| Stop, 400 km/h | 28.7 → 26.2 | 2.23 → 2.38 | 36 → 37 | 2.3 → 2.1 |
| Push, 250 km/h | 21.0 → 25.3 | 0.98 → 1.16 | 17 → 23 | 1.7 → **3.2** |
| Push, 320 km/h | 24.3 → 27.6 | 1.08 → 1.35 | 21 → 26 | 2.9 → **5.3** |
| Push, 400 km/h | 28.7 → 26.2 | 1.35 → 1.26 | 30 → 24 | 6.9 → 4.2 |

The stop takes longer because the roll held is faster (the yaw share at 65° is sin 65 = 0.91, against 0.87 at 60°) and the braking yaw is no larger.

### Case set

- 500 km/h full-aft VV roll: AoA 59 → 64°, VV roll R / L 16.2 / −9.3 → 21.0 / −7.0 deg/s, sideslip 7.3 → **2.7°**.
- Falling leaf and leaf reversal (their case sets AoA 70 itself), loaded rolls, 1 g rolls: identical.

## Not checked

- The 0.3.6 notes put the fin stall and the rudder-set flip at 63–67° AoA. The 65° runs above did not show the reversed set or a sideslip jump, but the in-game 0.3.13 wrong-way roll was not reproduced on the bench either, so this is the edge to watch in flight.
- STOVL is unaffected (StovlAoALimit 24 caps it).
