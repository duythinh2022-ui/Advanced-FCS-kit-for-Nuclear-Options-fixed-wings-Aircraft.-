# Vortex FCS 0.3.14: lateral governor margins as on the F-22E

17 Nov 2026. Benched, not flown.

Tom's requests:
- 19:51: "Seems like the velocity vector roll braking margin is a bit loose. Check that log again".
- 19:53: "I want the margin to be the same as the raptor. Corkscrews and falling leafs at max aoa feels decently fast but mushy".

## What the logs show (19:24 and 19:32, 0.3.13)

Velocity-vector (VV) rolls at the 60° limit, released:
- **Stop time:** 1.4–2.7 s to stop.
- **Overrun:** up to 50–56° of bank past the release.

**Why the stop is slow:**
- The roll command brakes in about 0.4 s.
- The yaw is already saturated, though, so the airframe only gets about 21 deg/s² of yaw braking.
- At 60° AoA a VV roll is about 87 % yaw (r = p_V · sin α). The stop is therefore a yaw stop, and the yaw has no spare authority to make it.

**The 19:24 push at 152.2 s:**
- Release plus full push swept the AoA from +60 to −37° with the yaw rate still on.
- Sideslip reached −22.6°.

**Cause:**
- Above 45° AoA the governor's roll-rate cap was faded out by the Vortex-only 0.3.1 `LeafRateCapFadeLo/Hi` (45 / 60). That cap limits the roll rate to what a stick reversal can stop inside the sideslip margin.
- With the cap gone, the roll ran to whatever rate the yaw could reach, not to the rate it could stop.
- On top of that, the margins were wider than the F-22E's:
  - `RollStopSideslip` 6 against 4;
  - `PushoverSideslip` 8 against −1 (follows `RollStopSideslip`);
  - `RollPaceRateBlend` 0.65 against 0.41.

## Changes

Section 6 (Lateral governor) now reads as Tom's `Aryx_F22E_StrikeRaptor.FCS.cfg`:

| Key | 0.3.13 | 0.3.14 = F-22E |
|---|---|---|
| RollStopSideslip | 6 | 4 |
| PushoverSideslip | 8 | −1 (follows RollStopSideslip) |
| RollPaceRateBlend | 0.65 | 0.4084507 |
| LeafRateCapFadeLo / Hi | 45 / 60 | 45 / 45 (Hi ≤ Lo = off; the F-22E has no fade) |
| PullRollTrade, RollPaceRateSlip, RollGovernorPitch, RollStopSlipProbe | 0.7, 1, true, true | unchanged (already equal) |

**Migration:**
- `GovernorRevision` 6 moves each key once, and only while it still holds its old default exactly (6, 8, 0.65, and 45 / 60 for the fade pair).
- Hand-set values are kept.

**Code:**
- The shared governor code is unchanged; only Vortex defaults and the migration changed.
- No allocator or native change; ABI 2013001.

## Bench: 0.3.13 vs 0.3.14

All rows are benched, not flown.

### Max-AoA VV roll, held 3 s then released (60° AoA)

| Case | Held VV roll (deg/s) | Time to stop (s) | Overrun after release (deg) | Max sideslip after (deg) |
|---|---|---|---|---|
| Stop, 250 km/h | 24.8 → 21.0 | 3.01 → 2.53 | 52 → 32 | 4.3 → 2.0 |
| Stop, 320 km/h | 28.5 → 24.3 | 2.91 → 2.45 | 58 → 35 | 3.5 → 2.4 |
| Stop, 400 km/h | 29.8 → 28.7 | 2.80 → 2.23 | 56 → 36 | 3.4 → 2.3 |
| Release + full push, 250 | 24.8 → 21.0 | 1.10 → 0.98 | 24 → 17 | 4.3 → 1.7 |
| Release + full push, 320 | 28.5 → 24.3 | 1.21 → 1.08 | 29 → 21 | 3.4 → 2.9 |
| Release + full push, 400 | 29.8 → 28.7 | 1.33 → 1.35 | 33 → 30 | **5.8 → 6.9** (regression) |

**Bench vs game:**
- The bench rolls slower here than the game (game held 30–34 deg/s).
- The bench did not reproduce the game's −22.6° push sideslip with either build.
- Read these rows as the direction and size of the change, not as game numbers.

### Regression case set

| Case | 0.3.13 | 0.3.14 |
|---|---|---|
| Falling leaf, stop time | 2.02 s | **1.55 s** |
| Falling leaf, max sideslip | 5.2° | **3.5°** |
| Falling leaf, roll rate (achieved / commanded) | 40.5 / 42.5 deg/s | 29.8 / 29.9 deg/s |
| Leaf reversal, zero cross | 1.72 s | 1.57 s |
| 500 km/h full-aft VV roll, sideslip | 9.6° | **7.3°** |
| Asymmetric-load roll L, 330 km/h, bank in 5 s | 400° | **355°** (regression) |
| Asymmetric-load roll R, bank in 5 s | 419° | **382°** (regression) |
| Asymmetric-load roll L, bank in 5 s | 372° | 369° |

### Max-AoA roll onset (time to 20 deg/s VV roll, mean of 6 speeds / sides)

| Limit | 0.3.13 | 0.3.14 |
|---|---|---|
| +60° | 1.93 s, sideslip 5.0° | **2.28 s** (regression), sideslip 3.3° |
| +55° | 1.93 s, sideslip 5.2° | **2.49 s** (regression), sideslip 4.3° |
| −55° | 0.61 s, pV at 2 s 58 deg/s, sideslip 3.6° | 0.62 s, **44 deg/s** (regression), sideslip 2.5° |

### Mid-AoA held roll and reversal (AoA held 15–35°)

| AoA, km/h | Steady roll (deg/s) | Reversal zero cross (s) | Max sideslip (deg) |
|---|---|---|---|
| 15, 300 | 62 → 67 | 0.50 → 0.53 | 5.0 → 4.2 |
| 20, 300 | 51 → 58 | 0.60 → 0.76 | 3.5 → 3.2 |
| 25, 300 | 42 → 52 | 0.61 → 0.75 | 3.7 → 3.7 |
| 30, 300 | 37 → 48 | 0.68 → 0.83 | 3.8 → 3.8 |
| 35, 300 | 34 → 45 | 0.88 → **1.13** | 4.4 → 4.1 |
| 15, 450 | 64 → **61** | 0.46 → 0.45 | 4.9 → 5.0 |
| 20, 450 | 49 → 53 | 0.51 → 0.53 | 3.3 → 3.3 |
| 25, 450 | 42 → 49 | 0.58 → 0.61 | 3.1 → 3.8 |
| 30, 450 | 37 → 45 | 0.63 → 0.81 | 3.7 → 3.8 |
| 35, 450 | 34 → 43 | 0.73 → 0.85 | 4.3 → 4.3 |

## Reading it

**What got tighter:**
- The stops at max AoA (about 20° less overrun, 0.5 s quicker).
- The falling leaf: 0.5 s quicker stop, 1.7° less sideslip.
- The full-aft high-speed VV roll: 2.3° less sideslip.
- This is the "mushy" part: the roll now runs only as fast as the yaw can stop it inside 4°.

**What it costs:**
- Peak rate at max AoA:
  - leaf roll 40 → 30 deg/s;
  - +55 / +60 onset about 0.35–0.55 s slower;
  - −55 roll at 2 s 58 → 44 deg/s.
- Loaded rolls 9–11 % less bank in 5 s.

**Mid-AoA:**
- The steady roll rises 4–12 deg/s at 20–35° (15° at 450 km/h: 3 deg/s lower). The lower blend (0.41) leans less on the rate cap and more on pacing.
- Reversals are slower to cross zero at 300 km/h (up to 0.25 s), at the same sideslip.

**If it is now too slow at max AoA:**
- `RollPaceRateSlip` (F-22E 1) is the one-knob trade. Above 1 the cap aims past the margin and lets the roll run faster.
- Raising it moves the F-22E too only if set in both files.
