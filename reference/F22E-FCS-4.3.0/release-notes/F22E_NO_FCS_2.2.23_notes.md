# F-22E FCS 2.2.23: rolling-pull governor defaults 9 / 0.7

Base: 2.2.22. No control-law change; only two defaults.

## Change
| Dev setting (3. Lateral-directional) | Old default | New default |
|---|---|---|
| `PullSideslipMargin` | 6 deg | **9 deg** |
| `PullRollTrade` | 0 | **0.7** |

- **Why:** 6 / 0 was too tight. A full pull with the roll already running built AoA very slowly, and with the stick fully aft and full roll applied together from wings level it never reached 20 deg AoA within 4 s at 178-297 kt.
- **9 / 0.7 is already your setup**, and it is what every bench result since about 2.2.14 was run with.
- **Migration (`AirframeRevision` 7, once):** a Dev cfg that still holds 6 for the margin is moved to 9, and one that holds 0 for the trade is moved to 0.7. Any other value is kept. Your cfg (9 / 0.7) is unaffected.
- Without the Dev DLL, the jet uses the new defaults directly.

## Bench: 6 / 0 against 9 / 0.7
Same 2.2.22 control laws, both fins on, AB, 3 km.

**Full aft + full roll together from wings level, held 4 s** (178 / 243 / 297 / 335 / 378 kt):

| | 6 / 0 | 9 / 0.7 |
|---|---|---|
| Time to 20 deg AoA | not reached at 178-297 kt; 3.7-4.3 s above | 1.4-1.8 s |
| AoA 2 s after the pull, mean / max | 6.8 / 13.6 deg | 25.8 / 29.4 deg |
| Sideslip worst, mean / max | 6.3 / 7.4 deg | 4.4 / 5.2 deg |
| Velocity-vector roll rate, mean over the run | 149 deg/s | 44 deg/s |
| Bank change at 2 s, mean | 178 deg | 107 deg |

- With the trade, the roll slows so the AoA can come up inside the sideslip margin. Without it, the governor held the AoA instead and the jet just rolled.

**Full roll established, then a pull of half or full stick** (119-324 kt):

| | 6 / 0 | 9 / 0.7 |
|---|---|---|
| Full-stick pull, time to 20 deg AoA | 2.4 s at 119 kt; 3.7-4.6 s at 205-324 kt; not reached at 157 kt | 1.9-2.7 s |
| Sideslip worst, mean / max | 4.5 / 5.5 deg | 4.8 / 6.1 deg |
| Velocity-vector roll rate, full-stick pull | 67-214 deg/s | 59-159 deg/s |
| Half-stick pull | nearly identical (AoA at 2 s within 2.4 deg, roll within 30 deg/s) | |

**Suite:** S1 worst / mean pitch dip -4.1 / -2.2 -> -4.0 / -2.0 deg/s. S4 at 297 kt: sideslip 5.1 -> 6.1 deg, pitch rate during the roll 9.3-16.5 -> 14.3-21.4 deg/s. Everything else identical.

## Not verified
- Not flown as a fresh install (your jet already flies these values).
- The migration runs in the plug-in's config load, which the bench doesn't exercise. Check `LogOutput.log` for `F-22E FCS 2.2.23 defaults: PullSideslipMargin 9, PullRollTrade 0.7.` once.
