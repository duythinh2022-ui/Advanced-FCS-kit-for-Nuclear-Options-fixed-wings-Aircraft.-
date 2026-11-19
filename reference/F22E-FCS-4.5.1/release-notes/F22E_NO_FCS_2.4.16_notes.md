# F-22E FCS 2.4.16

Base: **2.4.15**. One fix: the rolling-pull sideslip governor and `PullRollTrade` now also act with **`PitchRateCommandOnly = true`**.

Bench only. Not flown.

## What was wrong
- **Your config:** `PitchRateCommandOnly = true` (main cfg). With the gear up and Stability Assist on, that puts the FCS in `FcsMode.Rate`. The logs show it as mode 6.
- **The gate:** the AoA-sweep yaw limit block runs the 2.1.8 sweep limit, the 2.2.7+ rolling-pull governor (margin, feed-forward boost, 2.2.28 servo) and the `PullRollTrade` roll limit. That block, plus the 2.2.11 trade feed-forward, was gated on `Mode == FcsMode.AoAG`. So under the rate law none of it ran.
- **Your settings were fine:** margin 9, trade 0.7, `PullFFBoostMax` 4 and `AoASweepYawLimitPositive` on. They were just never reached.

**Logs, 28 Oct 22:24 and all six 29 Oct logs** (every one in mode 6):

| | Rolling-pull rows | `sweepLim` | `slipGovPull` | `pullTrade` | `pullFFBoost` |
|---|---|---|---|---|---|
| Value in every row | 4,200+ | 0 | 1 | 0 | 1 |

**Worst rolling pulls on 29 Oct.** The pitch-rate command stayed at the full 60 deg/s in all of them:

| Log, t | IAS | AoA | Sideslip |
|---|---|---|---|
| 11:12, 27.8 s | 156 kt | -4 -> 32 | 21.7 |
| 11:12, 41.0 s | 117 kt | -22 -> 40 | -23.4 |
| 11:14, 49.5 s | 223 kt | 6 -> 25 | 22.3 |
| 10:54, 285.6 s | 226 kt | 10 -> 35 | -18.3 |

## Change
- **Both gates** (the sweep / governor block and the trade feed-forward) are now `Mode == AoAG || Mode == Rate`.
- **Unchanged:** the code inside. It only clamps the pitch-rate command (`num34`) from measured state, so it works the same way under either law.
- **Still off:** MPO, gear-down rate, AI.

## Bench
- **Setup:** 2.4.15 sources with your cfg stub, airframe from Aryx 1.0.4, `DisableTVC = false`, full throttle, 3 km. The table compares the rate law before and after, with the AoA/G law as the reference.

**A: full aft + full roll together from wings level**

| IAS (km/h) | Sideslip worst, Rate 2.4.15 / Rate 2.4.16 / AoAG | AoA at 2 s | Time to 30 deg | Mean vel.-vector roll, first 2 s |
|---|---|---|---|---|
| 250 | 9.6 / 6.8 / 6.9 | 57 / 45 / 45 | 0.9 / 0.9 / 0.9 s | 33 / 30 / 29 |
| 320 | 14.7 / 6.9 / 6.8 | 51 / 43 / 43 | 0.9 / 1.0 / 1.0 s | 50 / 45 / 45 |
| 400 | 17.0 / 5.5 / 5.6 | 39 / 51 / 51 | 1.2 / 1.1 / 1.1 s | 67 / 50 / 50 |
| 500 | 17.3 / 4.4 / 4.4 | 33 / 46 / 46 | 1.9 / 1.2 / 1.2 s | 90 / 62 / 62 |
| 600 | 12.1 / 5.8 / 5.9 | 27 / 30 / 30 | 2.3 / 2.0 / 2.1 s | 122 / 106 / 106 |

**B: full roll established for 1 s, then full aft**

| IAS (km/h) | Sideslip worst | AoA at 2 s | Time to 30 deg | Mean vel.-vector roll, first 2 s |
|---|---|---|---|---|
| 250 | 12.0 / 2.7 / 2.7 | 38 / 40 / 40 | 1.6 / 1.5 / 1.6 s | 89 / 71 / 72 |
| 320 | 16.3 / 2.0 / 2.0 | 32 / 40 / 40 | 1.9 / 1.5 / 1.5 s | 119 / 74 / 74 |
| 400 | 17.5 / 2.1 / 2.0 | 28 / 40 / 40 | 2.2 / 1.5 / 1.6 s | 144 / 85 / 86 |
| 500 | 14.3 / 3.5 / 3.6 | 18 / 34 / 34 | 2.8 / 1.7 / 1.7 s | 188 / 113 / 115 |
| 600 | 10.3 / 4.7 / 4.6 | 22 / 25 / 24 | not reached / 2.9 / 3.0 s | 197 / 145 / 145 |

- **Rate law now matches the AoA/G law** to within 0.1-0.7 deg AoA and 0.1 deg sideslip in every case.
- **Same cost as on the AoA/G law:** the roll is slowed by the 0.7 trade while the AoA comes up. That is your existing tuning.
- **At 250 km/h with a pull from wings level,** the AoA at 2 s is lower (57 -> 45 deg). That is the governor holding the margin where 2.4.15 ran to 9.6 deg.

**Regression, rate law:**

| Case | Result |
|---|---|
| Full aft, no roll (300 / 500 km/h) | identical (max difference 0.000) |
| Full roll, no pull (300 / 500 km/h) | identical |
| Pull, roll, then full push (300 / 500 km/h) | sideslip 13.3 -> 7.3 and 11.7 -> 11.1 deg |

- **Why the push case changed:** the 2.1.8 nose-down sweep limit sits in the same block, so it now acts under the rate law too, as it does on the AoA/G law.

## Not verified
- Not flown.
- The bench yaw follows less well than the jet. The in-game sideslip in these pulls was 17-23 deg against 12-17 on the bench, so the in-game gain may differ in size.

## Install
- **Main:** `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.4.16.
- **Dev DLL:** unchanged (2.4.14).
- **Backup:** 2.4.15 is `…\Aryx_F22E_StrikeRaptor_FCS.dll.2.4.15.bak`.
- **Release:** `C:\Games\NO mods\F22E-FCS-2.4.16-release`.
- **Check:** `LogOutput.log` should show `F-22E FCS 2.4.16`. In a rolling pull, the CSV's `sweepLim`, `slipGovPull` and `pullTrade` should move off 0 / 1 / 0.
