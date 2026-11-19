# T/A-30 Compass FCS 0.4.1 (12 Nov 2026)

Base: 0.4.0. Patch bump: **no law change** (bench output identical to 0.4.0). A performance port to bring every FCS mod to the same level.

**Status:** compiled, benched (full suite, asymmetric-load matrix, loaded-reversal set), run on the game's Mono under Wine. **Not flown** (nothing to fly: same laws).

## What changed
The coupled asymmetric-load check (the LP that works out how much roll is left each way with the load held, pitch held and no sideslip acceleration) is now as fast as on the newest jet:
- **Native LP:** the simplex solve runs in the Rust core (`cmp_fcs_cpl`), with the C# solver as fallback (`NativeCoupledCap`, on).
- **Grid cache:** each AoA point's surface figures come from the sweep's per-tick geometry in one kernel call, instead of being rebuilt per point (`CplGridCache`, on).
- **One-side solve:** with the design sideslip on, the zero-sideslip case is solved for feasibility only (phase 1) and each design case only on its own side: 3 phase-1 + 2 phase-2 solves per point instead of 3 + 6. The skipped solves' results were thrown away before, so the figures are the same (`CplOneSide`, on).
- Core ABI 3004001; core sha256 `180cc69786871cfb...`, embedded in the plugin DLL.

## Bench
- **Identity:** per-case CSVs identical 413 / 413 against 0.4.0 with the new path on, and again with each switch off; suite tables identical.
- **Both-ways check:** `NativeAllocatorCheck = 2` solved every coupled-check LP natively and in C# and compared: 467,226 calls, all identical.
- **Cost (bench, .NET, 900 kg on `wing1_L` (about 32 cm), full aft pull at 300 km/h, 3 runs):** asymmetric sweep 221-242 µs per tick, against 357-425 µs with the three switches off (-41 %). On the game's Mono the C# LP is slower than on .NET (50 µs native vs 169 µs C# per LP solve), so the saving in game should be larger.

## Mono (game runtime, Wine)
`jetcheck`: allocator self-test 200 / 200, coupled-cap self-test 50 / 50 (native vs C#, including the one-side calls), PASS. All six FCS DLLs loaded together in one Mono process: each core loads from its own cache folder, PASS.

## Switches (bench statics)
`NativeCoupledCap`, `CplGridCache`, `CplOneSide`: off = 0.4.0's path for that piece.

## Install
- `BepInEx\plugins\Compass_TA30.FCS\Compass_TA30_FCS.dll`; 0.4.0 kept as `Compass_TA30_FCS.dll.0.4.0.bak`.
- Dev DLL unchanged.
- Release folder: `C:\Games\NO mods\Compass-FCS-0.4.1-release`.
- Mod manager: update its record of this mod's version, or its modpack may put an old DLL back. Restart the game to load it.
