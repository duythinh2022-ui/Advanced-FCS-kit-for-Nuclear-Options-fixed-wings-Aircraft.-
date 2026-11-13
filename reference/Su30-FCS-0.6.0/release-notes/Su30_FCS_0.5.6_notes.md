# Su-30VXN FCS 0.5.6 (12 Nov 2026)

Base: 0.5.5. Patch bump: **no law change** (bench output identical to 0.5.5). A performance port to bring every FCS mod to the same level.

**Status:** compiled, benched (full suite, asymmetric-load matrix, loaded-reversal set), run on the game's Mono under Wine. **Not flown** (nothing to fly: same laws).

## What changed
The coupled asymmetric-load check (the LP that works out how much roll is left each way with the load held, pitch held and no sideslip acceleration) is now as fast as on the newest jet:
- **Native LP:** the simplex solve runs in the Rust core (`su30_fcs_cpl`), with the C# solver as fallback (`NativeCoupledCap`, on).
- **Grid cache:** each AoA point's surface figures come from the sweep's per-tick geometry in one kernel call, instead of being rebuilt per point (`CplGridCache`, on).
- **One-side solve:** already in 0.5.0 (the zero-sideslip case solved for feasibility only, each design case only on its own side); now also in the native core.
- Core ABI 5005006; core sha256 `78d19a801cbd005a...`, embedded in the plugin DLL.

## Bench
- **Identity:** per-case CSVs identical 382 / 382 against 0.5.5 with the new path on, and again with each switch off; suite tables identical.
- **Both-ways check:** `NativeAllocatorCheck = 2` solved every coupled-check LP natively and in C# and compared: 526,376 calls, all identical.
- **Cost (bench, .NET, 800 kg on `Left_wing_middle`, full aft pull at 300 km/h, 3 runs):** asymmetric sweep 177-202 µs per tick, against 290-321 µs with the native LP and grid cache off (-37 %). On the game's Mono the C# LP is slower than on .NET (63 µs native vs 164 µs C# per LP solve), so the saving in game should be larger.

## Mono (game runtime, Wine)
`jetcheck`: allocator self-test 200 / 200, coupled-cap self-test 50 / 50 (native vs C#, including the one-side calls), PASS. All six FCS DLLs loaded together in one Mono process: each core loads from its own cache folder, PASS.

## Switches (bench statics)
`NativeCoupledCap`, `CplGridCache` (the one-side solve was already in 0.5.0): off = 0.5.5's path for that piece.

## Install
- `BepInEx\plugins\Su30_VXN.FCS\Su30_VXN_FCS.dll`; 0.5.5 kept as `Su30_VXN_FCS.dll.0.5.5.bak`.
- Dev DLL unchanged.
- Release folder: `C:\Games\NO mods\Su30-FCS-0.5.6-release`.
- Mod manager: update its record of this mod's version, or its modpack may put an old DLL back. Restart the game to load it.
