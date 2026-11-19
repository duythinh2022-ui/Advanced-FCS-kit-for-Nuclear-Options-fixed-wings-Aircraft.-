# FS-12 Revoker FCS 1.3.0 (12 Nov 2026)

Base: 1.2.0. Minor bump: one law change (the coupled asymmetric-load check now also runs with the nozzles locked or the thrust low) plus a performance port.

**Status:** compiled, benched (full suite + both asymmetric-load matrices + loaded-reversal set), run on the game's Mono under Wine. **Not flown.**

## 1. Coupled check with the nozzles locked / low thrust (`CplNoTvc`, on)
**Before:** 1.2.0 used the coupled check (roll each way, pitch held, no sideslip acceleration, every surface and both nozzles counted) only while vectoring with the thrust up. With the nozzles locked (DisableTVC), cut out by speed, or under `LowThrustFixedAoATW`, it fell back to 1.1.1's one-axis check: limit 10-14 deg with 30 cm of offset.

**Now:** the same LP runs there without the nozzles as effectors (their thrust at its present angle stays in the load), and the nose-down cap reads the LP's held-load pitch figure (`PitchHeld`: the most nose-down pitch acceleration with the load's roll and yaw held) with a margin that scales with dynamic pressure (Compass 0.4.0 / Su-30 0.5.0 pieces). In plain words: the lateral limit and the "can I still get the nose down" limit now come from the same model, so one can't open while the other falls under the jet.

**Separate share:** new dev setting `AsymCoupledReserveMaxNoTvc` (0.8; the vectoring path keeps `AsymCoupledReserveMax` 0.7). At 0.7 a full pull at 500-550 km/h with the nozzles locked ran 6-9 deg past the limit with 13 deg of sideslip; at 0.8 it stays within 2 deg, sideslip 1.4 deg or less.

**Nozzles still free at low thrust (`CplNoTvNeutral`, on):** when the nozzles can still move (low thrust, vectoring not locked) the load takes the thrust at the nozzles' neutral and the nose-down figure gets the nozzles' own nose-down range, but only with a load on. The present angle is the controller's own output: on the Ifrit it fed back into the check and flipped the cap 10 <-> 34 deg every 1.5 s. On this jet the bench idle cases were steady either way (within 0.1-0.3 deg).

### Bench, 30 cm of offset (2,000 kg on `wing1b_L`), full aft stick, 20 s, after t = 6 s
| case | 1.2.0 limit / AoA | 1.3.0 limit / AoA | sideslip max 1.2.0 -> 1.3.0 |
|---|---|---|---|
| nozzles locked, AB, 250-600 km/h | 12-16 / 12-16 | 12-21 / 12-21 | 0.2 -> 0.2-0.3 |
| nozzles locked, half throttle 300 / 450 | 10-12 / 10-12 | 12-20.5 / 12-23 | 0.1-0.2 -> 0.2-0.4 |
| nozzles locked, idle 300 / 450 | 10-14 | 23.4-24.5 | 0-0.2 |
| vectoring on, idle (low thrust) 300 / 450 | 10-14 | 23.5-24.8 | 0-0.1 |
| nozzles locked, 16 cm (1,160 kg) 450 | 15-19 | 21-25 | 0.2-0.3 |
| nozzles locked, 8 cm (560 kg) 300 | 20-36, raw limit jumping 12 <-> 70 (6 jumps) | 29-32, no jumps | 2.0 -> 1.2 |
| vectoring, throttle step AB -> idle -> AB, 350 | 10-34 (forced nose-down) | 24-28 steady | 2.3 -> 2.1 |
| vectoring, AB 300 / 600 | 27.8-28.6 / 27.4-28.0 | same | same |

- Roll reversals on the limit with the nozzles locked: same roll rates, sideslip 2.5-3.4 deg (1.2.0: 2.9-4.2).
- Loaded-reversal set (88 cases): essentially unchanged, hitch 0; roll spam sideslip 1.88 mean / 2.95 worst (1.2.0: 1.95 / 3.03).
- Damage suite: identical tables.

### Known limit (bench)
Nozzles locked + afterburner relit below about 70 m/s (`nt_thrcut_350`: AB -> idle for 6 s -> AB, 30 cm): the limit opens to 24 deg and sideslip reaches 19 deg (1.2.0, which held 10-14 deg and flew faster: 4.8 deg). Nothing in the LP can hold that load there at any AoA; the thrust about the offset CG does not fall with speed. Avoid relighting with the nozzles locked when slow and heavy on one side, or set `AsymCoupledReserveMaxNoTvc` higher.

## 2. Performance: native coupled-cap LP + grid cache + one-side solve
**No law change in this part** (bench per-case CSVs identical with it on and off).
- The coupled check's simplex LP now runs in the native Rust core (`rev_fcs_cpl`, ported from the Ternion 0.3.0 `cpl.rs`), with a C# fallback (`NativeCoupledCap`, on).
- Its surface points are computed once per sweep tick on the cached geometry with one kernel call (`CplGridCache`, on).
- With the design sideslip on, the zero-sideslip case is solved for its feasibility only and each design case only on its own side (`CplOneSide`, on): 3 phase-1 + 2 phase-2 solves per point instead of 3 + 6, same figures.
- ABI 2013000; core sha256 `0f4005379b415da9...`, embedded in the plugin DLL as before.

**Identity (bench, 1.3.0 laws):** native vs C# LP, grid cache on/off, one-side on/off: per-case CSVs identical 416 / 416 each; `NativeAllocatorCheck = 2` ran both and compared 443,752 LP calls: all identical.

**Cost (bench, .NET, 30 cm load, full aft pull at 300 km/h, 3 runs):** asymmetric sweep 290-302 µs per tick, against 494-519 µs with the three switches off (-41 %). On the game's Mono the C# LP is slower than on .NET (Mono, Wine: 46 µs native vs 149 µs C# per LP solve), so the saving in game should be larger.

## Mono (game runtime, Wine)
`jetcheck`: allocator self-test 200 / 200, coupled-cap self-test 50 / 50 (native vs C#, including the one-side calls), PASS. All six FCS DLLs loaded together in one Mono process: each core loads from its own cache folder, PASS.

## New dev settings
- `Asymmetric load / AsymCoupledReserveMaxNoTvc` (0.8).
- Bench-only statics: `CplNoTvc`, `CplNoTvNeutral`, `NativeCoupledCap`, `CplGridCache`, `CplOneSide` (all on; off = 1.2.0's path for that piece).

## Install
- `BepInEx\plugins\Revoker_FS12.FCS\Revoker_FS12_FCS.dll` 1.3.0; 1.2.0 kept as `Revoker_FS12_FCS.dll.1.2.0.bak`.
- Dev DLL unchanged (1.2.0's).
- Release folder: `C:\Games\NO mods\Revoker-FCS-1.3.0-release`.
- Mod manager: its record may still list an older version; update it there, or its modpack may put an old DLL back. Restart the game to load it.
