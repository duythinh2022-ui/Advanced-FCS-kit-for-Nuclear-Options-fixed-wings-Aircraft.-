# F-22E FCS 2.5.14

Base: **2.5.13**. Two things from the 13:53 / 14:12 logs: the margin schedule by AoA is retired (one number), and the feed-forward roll trade spends the yaw the rudders are not using instead of a fixed 7.5 % share - the thing that was holding low-AoA rolls under ~115 m/s to 60-90 deg/s. Bench for the trade; the margin change is a definition.

## What the logs showed
- 13:53 (2.5.13, blend 0.41): the margin reset 15/20/20 -> 4/6/8 at 140 s cut sideslip max 11.0 -> 7.7, q95 6.2 -> 4.2, time above 8 deg 2.8 s -> 0, at no roll-rate cost in the 22-40 deg bands (34 -> 33, 28 -> 37 deg/s) - at blend 0.41 the capped target carries 41 % of the weight, so the margin trims rather than rules.
- Both logs: full lateral stick at 2-10 deg AoA, 85-115 m/s: `pSmax` = `pullTrade` throughout - the FF roll trade capped the roll at 60-90 deg/s against a `pMax` of 120-145, with the pitch stick centered (`sp` 0, q ~ 0). The AoA-G law's own return toward 1 g after a pull is a 5-10 deg/s AoA sweep, and the trade's yaw budget for any sweep was a fixed `PullTradeFFK` x `SweepPosK` = 7.5 % of the authority (~5 deg/s^2 at 100 m/s), tuned at level 1 behind a 180-230 kt fade-in that level 1+ dropped. Sideslip in those rolls < 2.6 deg, `yawSat` 0: the yaw was not short.
- A check for the "lateral authority goes bad past 8-9 deg of sideslip" premise across today's logs (velocity-vector roll achieved / asked, by sideslip band): 0.87-1.02 up to 9 deg at 25-70 deg AoA, nothing past 9 to judge by. The fins' stall bound (local AoA ~ 0.5 alpha + 0.9 beta) would say the fins are stalled at 35 deg AoA with no sideslip, yet the jet rolls fine there - the fins do not carry lateral authority up there. No physics term for the margin from this data; the knee flight (14:12 log) is the next input.

## Changes
1. **`RollStopSideslip` is the margin at every AoA.** `RollStopSideslip50` / `65` are no longer read (keys kept so configs load; descriptions say so). With your 4 that is 4 everywhere instead of 4 / 6 / 8.
2. **Trade share from free rudder.** The sweep may spend `PullTradeFreeK` (0.35) of the yaw authority times (1 - rudder load, measured `stopSlipAbsF`), never less than the old 7.5 %; past the rudder fade the old share. Rudders at 30 %: 25 % of the authority -> the low-AoA rolls above are uncapped (bench, roll after a light pull: 330 km/h 53 -> 60 deg/s mean / 68 -> 91 peak, 400 km/h 78 -> 98 / 94 -> 138, sideslip unchanged 1.7-2.8). Rudders pinned in a rolling pull: the trade as before.
   - Cost, bench regression at blend 0.41 / margin 4: a few reversal-under-load cases give more sideslip (full aft + roll + reversal 500 km/h 3.2 -> 7.9; pull-through 450 3.5 -> 5.4; roll-then-pull-reversal 400 6.1 -> 4.7, the other way); rolling pulls 250-400 unchanged (5.1 / 8.2 / 7.2 vs 5.1 / 8.2 / 7.4); crossings unchanged. 0.5 was tried (more roll, 6.8-8.8 on those cases); 0.25 gave less roll for the same cost.
3. Dev-unlock DLL version string 2.5.14. No new keys.

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` + `meta.json` 2.5.14; `BepInEx\plugins\Aryx_F22E_StrikeRaptor_FCS_Dev.dll` 2.5.14. Backup `.dll.2.5.13.bak`. Release `C:\Games\NO mods\F22E-FCS-2.5.14-release`. LogOutput: `F-22E FCS 2.5.14`.
