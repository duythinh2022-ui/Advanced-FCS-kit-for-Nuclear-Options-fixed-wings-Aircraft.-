# Vortex FCS 0.5.18: control surfaces wobble in STOVL pedal turns

20 Nov 2026. On 0.5.17.

**Status: benched, not flown.**

Tom, 20 Nov:

> the control surfaces wobbles when i do turning in stovl.

## What the log shows

Log `Vortex_FCS_20261120_004950.csv`, Stock jet, 12.55 t, 48 kt, full pedal, 119–141 s and 147–151 s. The turn itself is steady: bank 17°, turn rate 6.5°/s. Underneath it the roll axis oscillates without stopping:

| | Amplitude |
|---|---|
| Frequency | 1.2–1.5 Hz |
| Roll rate | ±3.5°/s |
| Bank | ±0.4° (too small to feel) |
| Roll posts | ±0.92 (end to end) |
| Surfaces | ±21°, moving at their full 100°/s servo rate |

So the jet barely moves, but the posts and surfaces work flat out. That's the wobble you saw.

## Cause

The pedal's full yaw rate is the yaw authority × `StovlYawStopTime`: the rate the yaw axis can start and stop within that time.

1. The yaw authority estimate is worked out every tick from the effectors' windows and tables. It moved ±5 % in step with what the roll axis was doing with the same posts and surfaces (correlation 0.97 with the post command).
2. In a pedal turn, the bank target follows the turn rate: g·tan(bank) = u × turn rate.
3. So a deeper bank lowered the yaw authority, which lowered the full-pedal turn rate, which lowered the bank target. The delay was 2 ticks, and the target moved about 2.6° per degree of bank.

That is a feedback loop through the bank loop, and with the posts' and surfaces' lag it oscillated.

## Fix (`StovlYawRateMeanAuthority`, dev file, on; false = 0.5.17)

The full pedal rate now uses the yaw authority **averaged over `StovlYawStopTime`** (a low-pass with that time constant) instead of this tick's value.

- **Why this is the physics, not a filter on top:** a stop sized to take `StovlYawStopTime` uses the authority over that whole time, so the mean over that time is the number that counts.
- **No new number:** the time constant is the setting the rate is already built on (1.25 s).
- **Yaw loop's own stops** still use the authority now.
- At the 1.47 Hz of the wobble the averaging cuts the loop gain about 12 times.

## Bench

**I missed this before:** the existing bench set already had the wobble in K45 (Buffed 13.8 t, 45 kt, full pedal).

Pedal turns (aileron split = left minus right aileron, peak to peak per second of the held turn):

| Case | | Aileron split in the held turn | Post std | Steady bank | Turn rate | Bank peak at turn-in | Skid |
|---|---|---|---|---|---|---|---|
| K45 | 0.5.17 | 30–49° every second | 0.52 | 23.9° | 9.69°/s | 24.0° | 0.59 m/s |
| K45 | **0.5.18** | 8° at entry, then 0–5° | 0.11 | 23.9° | 9.79°/s | 24.9° | 0.59 m/s |
| K30 | 0.5.17 | 25° once, then 0 | 0.15 | 21.9° | 13.25°/s | 21.9° | 0.80 m/s |
| K30 | **0.5.18** | 0–6° | 0.09 | 21.9° | 13.43°/s | 22.9° | 0.85 m/s |

**Side kick test:** a 3 m/s side kick in the middle of a 48 kt full-pedal turn (Stock 12.55 t, Tom's case).

| | Aileron split, per 0.5 s after the kick |
|---|---|
| 0.5.17 | 41, 38, 32, 46, 45, 11, then about 1° |
| 0.5.18 | 41, 33, 3, 4, 1, then 0 |

The oscillation used to grow back for 2.5 s; now it dies in about 1 s.

**Cost:** at the turn-in the bank goes about 1° past its steady value for about 2 s (K45: 24.9° for 24.0°; K30: 22.9° for 21.9°). The averaged authority lags the rising one, so the turn rate creeps up a little.

**Other sets:**

- **12-case set:** only the pedal cases move (K30, K45, HY; heading within 1.6°). With the switch off, all 12 are byte for byte 0.5.17.
- **Regression set:** only h_s1288 changes, for the better: yaw reaches 90 % at 1.78 s (was 2.68 s), height −0.07 / +0.05 m (was −0.10 / +0.06). Its yaw stop is 1.15 s after release (was 1.07). h_b15 moves 0.01 m.
- **Stock 13.68 t speed-floor case:** unchanged. There the pedal rate is limited by the turn's side-acceleration share, not by the yaw authority.

## Tried and dropped

Before finding the cause I tried letting the tilt governor bring the bank back for free up to the bank the current lift already holds. In the log, the governor's ratchet showed up as a symptom: bank metered out at about 5°/s and back in at up to 60°/s. That change made no measurable difference in the bench, so it is not in this build.

## Please fly

Repeat a full-pedal turn at 45–50 kt. If the surfaces still move, the log's `stYawLim` column should now be nearly flat while the pedal is held.
