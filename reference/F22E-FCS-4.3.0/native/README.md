# Native core (F-22E FCS 4.1.0)

`fcs_core/` is Rust: `F22E_FcsCore.dll` for the game, `libF22E_FcsCore.so` for the bench. It holds

- `Allocator.cs`'s solve (4.0.0), plugin side `Allocator.NativeSolve`;
- the grid kernels (4.1.0, `f22e_fcs_grid`): one surface's moment at every deflection of its grid in one airflow. Mode 0 fills the
  surface's table (`SurfaceEffector.EvalCompute`'s loop of `Eval`), mode 1 returns the min / max the asymmetric-load sweep keeps
  (`FcsController.AsymSweepPoint`'s capacity loop). Plugin side `SurfaceEffector.GridMomentsNative` / `GridRangeNative`; the
  rotations (sin / cos) stay in C# (`SurfaceEffector.GridGeom`).

The C# code is the reference; the library must return the same bits wherever C# float arithmetic is strict 32-bit.
The wrapper is `Aryx_F22E_StrikeRaptor.FCS/FcsNative.cs`. At load it runs a self-test of the kernels against a C# twin
(`FcsNative.GridTwin`); a failure switches the kernels off and leaves the solve on.

## Rules for changing it
- Any change to `Allocator.cs` (`SolveInner`, `ReachFrom`, `SolveCore`, `PairedSearch`, `SolveLinear`, the cost terms) or to
  `Effector.MAt` has to be made in `fcs_core/src/lib.rs` as well, expression for expression: same operand types, same order,
  same float / double conversions. No transcendental functions, no reordering, no fused multiply-add.
- A new field the solve reads goes into `EffN` or `AllocIo` on both sides (`lib.rs` and `FcsNative.cs`), and `ABI` / `Abi` is bumped.
- Any change to `SurfaceEffector.Eval`, `EvalFlow`, `EvalFlowAt`, `EvalFlowGrid`, `Camber.Foil` / `FoilPre`, `AirfoilTable.Get` /
  `Read`, `Effector.ToAero`, or to the sweep's capacity loop (the min / max, `AccRoll`, `AccYaw`) has to be made in `grid_point` /
  `grid_run` (`lib.rs`) and in `FcsNative.GridTwin` as well. The kernels may call `atan2` (written as C# does:
  `(float)Math.Atan2((double)y, (double)x)`) and nothing else from a maths library. `GridIn` changes on both sides, and in
  `verify/harness.cs`, with an `ABI` bump.
- Then run the checks below. Zero differences on the bench is the pass mark.

## Build
```
bash native/build.sh          # the game's DLL  -> fcs_core/target/x86_64-pc-windows-gnu/release/F22E_FcsCore.dll
bash native/build.sh linux    # the bench's .so  (regen.sh does this and copies it into bench/bin)
```
Built with Rust 1.99.1, target `x86_64-pc-windows-gnu`, mingw-w64 gcc 13 as the linker. No crates are downloaded.
Two builds of the same source differ in a few header bytes (the linker's time stamp): ship the build that was tested.
The DLL imports only Windows system DLLs (kernel32, msvcrt, ntdll, bcryptprimitives, userenv, ws2_32, api-ms-win-core-synch).

## Check on the bench (strict float arithmetic: must be bit-identical)
```
bash bench.sh suite base NativeAllocator=0
bash bench.sh suite nat                          # default: native, sampled checks
bash bench.sh suite chk NativeAllocatorCheck=2   # every solve solved both ways and compared
bash bench.sh diff base nat
```
`FCS_NATIVE_STATS=<file>` makes each bench run append its counts (native solves, solved both ways, identical, differences, kernel calls).
4.0.0: 119 / 119 per-case CSVs identical to 3.2.1 in all three; 427,582 solves compared, 0 differences.

4.1.0 (defaults are now `NativeAllocator = 2`, `NativeAllocatorCheck = 0`; on the bench that is the native core everywhere):
```
bash bench.sh suite s410                                           # solve + both kernels native
bash bench.sh suite s410off NativeSweep=false NativeTables=false   # solve native, kernels in C#
bash bench.sh suite s410cs NativeAllocator=0                       # all C#
```
All three: 153 / 153 per-case files and 5 / 5 suite tables identical to 4.0.1, 4.0.0 and 3.2.1; the first with 427,582 native solves and
1,068,812 kernel calls, none left to C#.

## Check on the game's own Mono runtime (verify/)
The game's Mono carries C# floats in double (Unity passes `-O=-float32`), so there the C# solve and the library differ.
The harness measures by how much, and proves that with `NativeAllocator = 1` the plugin returns the same bits as the last release.

1. Record solves on the bench (a test copy, never the plugin source):
   ```
   cp -r <tree> rec && (cd rec && bash regen.sh) && python3 native/verify/record_patch.py rec/bench/fcs/Allocator.cs
   (cd rec/bench && dotnet build -c Release -o bin)
   FCS_REC=r1.bin dotnet rec/bench/bin/bench.dll one <case> NativeAllocatorCheck=2
   ```
2. Build the host and the harness (the harness against the game's `Managed` DLLs and `BepInEx.dll`):
   ```
   x86_64-w64-mingw32-gcc -O2 -o host.exe native/verify/host.c
   csc -nostdlib+ -unsafe+ -target:exe -out:harness.exe -r:mscorlib.dll -r:System.dll -r:System.Core.dll -r:netstandard.dll \
       -r:UnityEngine.CoreModule.dll -r:BepInEx.dll native/verify/harness.cs
   ```
3. Run under Wine with the game's `mono-2.0-bdwgc.dll`, a folder holding the game's `Managed` DLLs plus BepInEx's core DLLs,
   and a plugin folder holding the plugin DLL and `F22E_FcsCore.dll` side by side:
   ```
   wine host.exe <mono-2.0-bdwgc.dll> <Managed> <etc> -O=-float32 harness.exe <plugin.dll> <mode> r1.bin ...
   ```
   Modes: `managed` (`NativeAllocator = 0`), `auto` (1), `native` (2), `nativechk` (2, every solve compared by the plugin).
   `FCS_DUMP=<file>` writes every solve's outputs, to `cmp` two plugin versions. `-` instead of `-O=-float32` runs Mono's
   strict float mode (then everything matches the bench).

   Mode `kernbench` (4.1.0, no record files) runs the grid kernel against the plugin's C# twin on that runtime: agreement and time.

4.1.0: `auto` byte-identical to 4.0.0 / 3.2.1 on the 26,700 solves; self-test passes; `kernbench`: native 3.3 - 3.7 us against 15.7 - 17.1 us
for the C# twin per surface (31 deflections, host section), 4.6 - 4.7x; 19 % of the moment components bit-identical in the game's float
mode, 100 % with Mono's float32 on.

4.0.0, 26,700 solves from six manoeuvres: `auto` byte-identical to the shipped 3.2.1; `native` identical to the bench's C# on
all of them; `native` against the game's C#: 7.1 % identical, 68.4 % within 0.001 deg, 24.2 % equal-cost alternatives, 0.3 %
different; 137.5 us per C# solve, 16.4 us per native solve.
