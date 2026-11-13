# F-22E FCS 2.5.5

Base: **2.5.4**. From the first 2.5.4 flight (31 Oct 10:10 log, 326 s, level 3; margins 4/5/5 until ~155 s, then raised out of the way). Bench-only beyond what that log shows.

## What the 10:10 log said about 2.5.4
- **Better.** In the AoA/G law: 8 excursions past 5 deg in 265 s airborne (09:44 log: 16 in 328 s), worst 9.7, two of them the 65 deg AoA / 40 m/s deep stall.
- **The set margin is not what holds the sideslip.** Before ~155 s (margin 4/5/5): at 5-36 deg AoA above 100 m/s, |sideslip| 95th percentile 3.0, worst 4.8, 0.6 % of the time past 4. After (margin raised to ~15/20/20 - the margin cap effectively off, `stopSlipLim` never under `pSmax`): 2.9 / 3.6 / 0 %. The pace governor alone keeps it. That is the "retire the set margin" thread answered by the log; the defaults are left as they are for now (your call).
- **Gear down, in the air, nothing governed:** 51 s airborne gear-down, 20.9 deg of sideslip (210 s), 10 % of that time past 5 deg; `stopSlipLim` 999, `paceHold` 0, `yawSatPitch` 0 throughout - every lateral governor was gated to the AoA/G and rate laws.
- **Pitch rate in a corkscrew push-over was slow and the roll was not traded.** 268 s (full push + full roll from +9 deg at 104 m/s): the trade sat at 139-154 against a roll limit of 119 - it never acted, because it only existed for nose-up asks at positive AoA - so the AoA-sweep cap took 15-24 deg/s off the pitch-rate command instead, and a second later the pace window cut the roll to ~10-20 anyway. 315 s (68 m/s): the cap took up to 50 deg/s off (pitch-rate command -9 to -20 against a -60 ask) for 0.8 s. The order was pitch first, roll later - the opposite of the rule.

## Changes
1. **The roll trade works both ways and at either AoA sign** (`PullRollTrade`, feed-forward part, levels >= 1). The roll limit is set from the AoA rate the stick asks for, nose-up or nose-down, at positive or negative AoA: the roll whose coordinating yaw the yaw axis can build while the AoA moves at that rate (same formula and gains as before: `PullTradeFFK` 0.3 x `SweepPosK` 0.25 x margin/6 x yaw authority x cos AoA / AoA-rate ask, floor 30 deg/s, applied at `PullRollTrade` 0.7). The cut-an-established-roll band (`PullTradeCutA` 15 + 10 deg) now reads |AoA|. The AoA-rate caps are unchanged, so with the roll traded down they have little left to take: roll first, then pitch, as the rule says.
2. **The lateral governors act with the gear down in the air** (`LatGovMode`: AoA/G law, rate law, or gear-down rate law with no wheel on the ground, under the existing `GearDownSlipProtect` switch): margin cap, yaw-reserve governor, pace governor, both AoA-rate caps, the roll trade, the no-chase hold. On the runway nothing changes.
3. Version 2.5.5. No new config keys.

## Bench (calibrated plant; the negative-AoA side is known to over-read, see `claude/F22E_bench_negAoA_calibration_handoff.md`)
- Stop / reversal sweep and every positive-AoA scenario: identical to 2.5.4 (the trade already acted there).
- Push + roll 300 km/h: sideslip 9.5 -> **2.8**, AoA at 2 s -18 -> **-28** (the push is faster), peak roll 108 -> 60. Pull-then-push-over + roll 350: 3.4 -> 2.7, roll 44 -> 34. Push-then-pull-through + roll 350: 40.9 -> 15.3 (bench departure halved, still a bench artefact), 450: 9.1 -> 6.6. 1 g roll reversal 500: 1.9 -> 1.3 (the slight unload now trades a few deg/s of roll: 188 -> 184).
- 10:10 log replays with your sticks: 268 s push-over, 2.5.4 bench 42.6 (departs; game 6.3) -> 2.5.5 bench 10.4, roll 107 -> 66; 315 s: roll 103 -> 20 within 0.6 s and the push runs at -57 deg/s by 315.6 (game -45, held at -10..-20 for 0.4 s by the cap), sideslip 3.5 (game 9.7).
- Gear-down: not testable on the bench (no gear-down scenario); the change is a gate.

## Not verified
- Not flown. Watch: push-overs and pull-throughs under roll (the roll should now come down to ~30-60 deg/s as the push/pull starts and the pitch rate stay near the ask); gear-down rolls in the air; any high-speed rolling unload that now feels roll-limited (the trade acts on nose-down asks too - that is the one feel change at positive AoA).
- The cap still takes ~0.4 s to release after a push starts while the roll is coming down (it reads the roll the jet has, which the trade brings down at the trajectory's braking rate).

## Install
- `BepInEx\plugins\Aryx_F22E_StrikeRaptor.FCS\Aryx_F22E_StrikeRaptor_FCS.dll` and `meta.json` 2.5.5. Backup: 2.5.4 is `...\Aryx_F22E_StrikeRaptor_FCS.dll.2.5.4.bak`. Release: `C:\Games\NO mods\F22E-FCS-2.5.5-release`. LogOutput: `F-22E FCS 2.5.5`.
