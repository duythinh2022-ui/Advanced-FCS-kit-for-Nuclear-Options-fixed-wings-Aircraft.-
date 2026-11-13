# FS-3 Ternion FCS 0.3.1 (12 Nov 2026)

Base: 0.3.0. Patch bump: **no law change** (bench output identical to 0.3.0). A performance port to bring every FCS mod to the same level.

**Status:** compiled, benched (full suite, asymmetric-load matrix, loaded-reversal set), run on the game's Mono under Wine. **Not flown** (nothing to fly: same laws).

The Ternion had the native LP and the grid cache since 0.3.0; 0.3.1 adds the one-side solve and the `side` field of the core call, so its core is rebuilt (the self-test now also checks the one-side calls) and the bench damage table differs from 0.3.0 only in the source-path text of two failing damage cases.

## What changed
The coupled asymmetric-load check (the LP that works out how much roll is left each way with the load held, pitch held and no sideslip acceleration) is now as fast as on the newest jet:
- **Native LP (since 0.3.0):** the simplex solve runs in the Rust core (`tern_fcs_cpl`), with the C# solver as fallback (`NativeCoupledCap`, on).
- **Grid cache (since 0.3.0):** each AoA point's surface figures come from the sweep's per-tick geometry in one kernel call, instead of being rebuilt per point (`CplGridCache`, on).
- **One-side solve (new):** with the design sideslip on, the zero-sideslip case is solved for feasibility only (phase 1) and each design case only on its own side: 3 phase-1 + 2 phase-2 solves per point instead of 3 + 6. The skipped solves' results were thrown away before, so the figures are the same (`CplOneSide`, on).
- Core ABI 3003101; core sha256 `27393d13af6a22d0...`, embedded in the plugin DLL.

## Bench
- **Identity:** per-case CSVs identical 439 / 439 against 0.3.0 with the new path on, and again with the one-side solve off; suite tables identical apart from the source-path text in two failing damage cases.
- **Both-ways check:** `NativeAllocatorCheck = 2` solved every coupled-check LP natively and in C# and compared: 640,625 calls, all identical.
- **Cost (bench, .NET, 1,370 kg on `wing_2a_L` (about 21 cm), full aft pull at 300 km/h, 3 runs):** asymmetric sweep 247-274 µs per tick, against 345-433 µs with the native LP and one-side solve off (-37 %). On the game's Mono the C# LP is slower than on .NET (44 µs native vs 147 µs C# per LP solve), so the saving in game should be larger.

## Mono (game runtime, Wine)
`jetcheck`: allocator self-test 200 / 200, coupled-cap self-test 50 / 50 (native vs C#, including the one-side calls), PASS. All six FCS DLLs loaded together in one Mono process: each core loads from its own cache folder, PASS.

## Switches (bench statics)
`CplOneSide` (new here; the native LP and grid cache were already in 0.3.0): off = 0.3.0's path for that piece.

## Install
- `BepInEx\plugins\Ternion_FS3.FCS\Ternion_FS3.FCS\Ternion_FS3_FCS.dll` (the mod manager's nested folder). What was installed there was **0.2.0**, not 0.3.0 (0.3.0 had only gone to the release folder); kept as `Ternion_FS3_FCS.dll.0.2.0.bak`. So in game this jet also picks up everything since 0.2.0 (0.2.1-0.3.0: see their notes).
- Dev DLL unchanged.
- Release folder: `C:\Games\NO mods\Ternion-FCS-0.3.1-release`.
- Mod manager: update its record of this mod's version, or its modpack may put an old DLL back. Restart the game to load it.
