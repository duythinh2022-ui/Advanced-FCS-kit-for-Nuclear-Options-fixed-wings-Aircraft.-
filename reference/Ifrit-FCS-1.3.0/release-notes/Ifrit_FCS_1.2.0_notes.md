# KR-67 Ifrit FCS 1.2.0 (12 Nov 2026)

Base: 1.1.3 (the differential-braking release). Minor bump: law change. The asymmetric-load AoA limit now comes from the coupled check (as on the Revoker, Compass, Ternion and Su-30), with its native solver.

**Status:** compiled, benched (full suite, both asymmetric-load matrices, loaded-reversal set), run on the game's Mono under Wine. **Not flown.**

## 1. What changed
- **Coupled check** (`AsymCoupledCheck`, on): at each AoA the LP works out how much flight-path roll is left each way with the load held, pitch held and no sideslip acceleration; every surface counted, and with vectoring on and the thrust up both nozzle pairs (pitch and differential). The cap sits where the weaker side is down to `AsymCoupledReserveMax` (**0.75**) of the roll range. In plain words: 1.1.3 asked "can the ailerons alone hold this load?", 1.2.0 asks "can the whole jet hold it and still roll both ways?".
- **Nozzles locked / low thrust:** the same LP without the nozzles as effectors, with a nose-down cap from its held-pitch figure and a q-scaled margin (`AsymCoupledReserveMaxNoTvc` 0.8), as the Revoker 1.3.0.
- **Top-edge search** (Su-30 0.5.0 `FindTop`): at very low speed this jet's demand curve has a hump near 10 deg (bench, 8 cm, 37 m/s: 1.11 at 10 deg, 0.25 at the 65 deg the jet was flying). 1.1.3's first-crossing search put the cap on its 12 deg floor there and pushed the nose down through the hump; the cap is now the lower edge of the top over-threshold stretch.
- **Free nozzles at low thrust:** with a load on, the check takes the nozzles' thrust at neutral plus their nose-down range, not their present angle. The present angle is the controller's own output: at idle it made the cap flip 10 <-> 34 deg every 1.5 s (300 km/h idle pull, 30 cm) and run 6-10 deg past it.
- **No-gain skip:** when the held-pitch margin is short already at the lowest AoA, no pitch cap (cutting AoA buys nothing there).
- **Performance:** native LP (`ifr_fcs_cpl`, ABI 1012000), grid cache, one-side solve. Core sha256 `d2fe30b48af3b595...`, embedded in the plugin.

## 2. Bench, 30 cm of offset (2,450 kg on `wing1_L`) unless noted; full aft 20 s, after t = 6 s
| case | 1.1.3 limit / AoA | 1.2.0 limit / AoA | sideslip max 1.1.3 -> 1.2.0 |
|---|---|---|---|
| afterburner 250-600 km/h | 12-26, rising with speed; 600: 3.6 deg over | 16-20, steady, <= 0.2 over | 0.2-3.0 -> 0.1-0.8 |
| 16 cm (1,280 kg), 300 / 450 | 24-65, raw 12 <-> 65 (2 jumps each) | 22.5-26, steady | 1.6 -> 0.2 |
| 8 cm (630 kg), 300 / 450 | 65 | 58-65 | 5.3 -> 4.0 |
| half throttle 300 / 450 | 12 / 12-41 (1 jump) | 16.5-21 / 17-25 | 1.3 -> 2.0 |
| idle 300 / 450 | 10 | 20-24 | 0.3 -> 0.4 / 3.5 (at 23 m/s) |
| nozzles locked, AB 300 / 450 | 10 | 20-21 | 0.2 |
| throttle step idle -> AB -> idle, 250 | 10-24 (2 jumps) | 16-23 (1) | 0.8 -> 2.1 |
| roll then release, 450 | roll-off after release 0.1-0.8 deg/s | 1.4-1.8 mean, 3-10 max | |

- **Loaded reversals (88 cases):** AoA above the cap 28.5 -> 1.5 deg mean (1.1.3 did not hold its own cap). Roll spam sideslip 5.0 / 8.4 -> 5.3 / 8.5 deg mean / worst. By load: 21 cm better (5.3 -> 4.7 mean), 16 cm better (6.2 -> 4.7), **12 cm worse: 3-5 -> 6-8.5 deg**, because the limit moved from about 63 deg (1.1.3, uncapped) to about 31 deg, where this jet rolls 70 deg/s instead of 30-45 and its reversals are paced by the yaw. Two single reversals there (12 cm, a 60 deg/s roll toward the heavy wing reversed toward the light one) stop the roll in 0.4 s and then hold it near zero for the rest of the 2 s stick input: the yaw is saturated. The clean jet capped at 39 deg does the same (bench: 1.85 s to start the new roll, 5.8 deg sideslip).
- **Share choice:** 0.7 (Revoker's) left a 18-25 deg/s roll-off after a full roll at 450 km/h with 30 cm; 0.8 cut 8 cm loads from 65 to 37-40 deg with a jump. 0.75: limits above, roll-off 1-2 deg/s.

### Known limits (bench)
- Nozzles locked at half / idle throttle, or a throttle cut and relight: above about 55 m/s the limit holds 20 deg steadily; below that the nose-down cap falls to its 10 deg floor, and at 30-45 m/s over the top of a loop the sideslip reaches 5-9 deg (1.1.3, which held 10 deg throughout and stayed faster: about 1 deg). Relighting on the limit at 42 m/s with the nozzles locked rolled off with 9 deg of sideslip.
- An afterburner relight below about 40 m/s with the nozzles free opens the cap (no AoA helps at that speed) and the jet rolls off at 11-14 deg/s, as in 1.1.3.
- A pitch-cap dip at the start of a half-throttle pull at 450 km/h (22 -> 13 deg for 0.6 s) is 1.1.3's own nose-down figure (same in both).

## 3. Identity and performance
- `AsymCoupledCheck = false`: per-case CSVs identical to 1.1.3, 416 / 416, all tables identical apart from their tag line. Clean (no load) cases are identical with it on too.
- Native vs C# LP: identical results; `NativeAllocatorCheck = 2` compared every LP call.
- **Cost (bench, .NET, 30 cm, full aft at 300 km/h, 3 runs):** asymmetric sweep 167-187 µs per tick with the native LP, grid cache and one-side solve, against 352-401 µs with them off. Mono (Wine): 70 µs native vs 246 µs C# per LP solve.

## Mono (game runtime, Wine)
`jetcheck`: allocator self-test 200 / 200, coupled-cap self-test 50 / 50, PASS. All six FCS DLLs loaded together in one Mono process, PASS.

## New dev settings ("Asymmetric load")
- `AsymCoupledCheck` (true): false = 1.1.3's one-axis check, identical flying.
- `AsymCoupledReserveMax` (0.75), `AsymCoupledReserveMaxNoTvc` (0.8). Higher = lower cap, more roll left.

## Install
- `BepInEx\plugins\Ifrit_KR67.FCS\Ifrit_KR67_FCS.dll` 1.2.0; 1.1.3 kept as `Ifrit_KR67_FCS.dll.1.1.3.bak`.
- Dev DLL unchanged.
- Release folder: `C:\Games\NO mods\Ifrit-FCS-1.2.0-release`.
- Mod manager: update its record, or its modpack may put 1.1.3 back. Restart the game to load it.
