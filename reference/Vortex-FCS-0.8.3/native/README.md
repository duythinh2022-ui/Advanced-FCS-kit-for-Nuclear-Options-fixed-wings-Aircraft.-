# Native core (Revoker FCS 1.1.0)

`fcs_core/` is Rust: `Vortex_FcsCore.dll` for the game, `libVortex_FcsCore.so` for the bench. A port of the F-22E Strike Raptor FCS 4.1.0 core
(`native/fcs_core` in the F-22E source) to this jet's own C#. It holds

- `Allocator.cs`'s solve, plugin side `Allocator.NativeSolve` (`SolveDispatch`);
- the grid kernels (`rev_fcs_grid`): one surface's moment at every deflection of its grid in one airflow. Mode 0 fills the
  surface's table (`SurfaceEffector.EvalCompute`'s loop of `Eval`), mode 1 returns the min / max the asymmetric-load sweep keeps
  (`FcsController.AsymSweepPoint`'s capacity loop). Plugin side `SurfaceEffector.GridMomentsNative` / `GridRangeNative`; the
  rotations (sin / cos) stay in C# (`SurfaceEffector.GridGeom`).

This jet's differences from the F-22E's core: the velocity-frame lateral residual in `Allocator.E` (`slipFrame`, `slipSin`, `slipCos` in `AllocIo`); the coordinate pass's Newton candidate also for the yaw nozzles (`EffN.kind` 3 = `TvcYawEffector`); the table kernel also returns each grid point's normal force (`SurfaceEffector.FN`, `GridIn.outFN`); the sweep evaluates the stabilators at their `AsymStabTravel` deflections, so its geometry is built for that list (`FcsController.SwDeflections`) and reused from the table only when the deflections match.

The C# code is the reference; the library must return the same bits wherever C# float arithmetic is strict 32-bit.
The wrapper is `Revoker_FS12.FCS/FcsNative.cs`. At load it checks the library's ABI number and structure sizes and runs a self-test of the
kernels against a C# twin (`FcsNative.GridTwin`); a failure switches the kernels off and leaves the solve on; a missing or failing
library leaves everything on the C# code.

**Own names.** Every FCS mod loads in the same game process, so this one has its own DLL name (`Vortex_FcsCore.dll`), its own
`DllImport` name and its own export prefix (`rev_fcs_`). The plugin loads the DLL by its full path (`LoadLibraryW`) before the
first call. Checked on the game's Mono with all five FCS plugins loaded at once: each bound its own library.

## Rules for changing it (as the F-22E's)
- Any change to `Allocator.cs` (`SolveInner`, `ReachFrom`, `SolveCore`, `PairedSearch`, `SolveLinear`, the cost terms, `E`) or to
  `Effector.MAt` has to be made in `fcs_core/src/lib.rs` as well, expression for expression: same operand types, same order,
  same float / double conversions. No transcendental functions, no reordering, no fused multiply-add.
- A new field the solve reads goes into `EffN` or `AllocIo` on both sides (`lib.rs` and `FcsNative.cs`), and `ABI` / `Abi` is bumped.
- Any change to `SurfaceEffector.Eval`, `EvalFlow` / `EvalFlowAt`, `EvalFlowGrid`, `Camber.Foil` / `FoilPre` / `DeltaFrom`,
  `AirfoilTable.Get` / `Read`, `Effector.ToAero`, or to the sweep's capacity loop has to be made in `grid_point` / `grid_run`
  (`lib.rs`) and in `FcsNative.GridTwin` as well. The kernels may call `atan2` (written as C# does) and nothing else from a maths
  library.
- Then run the checks below. Zero differences on the bench is the pass mark.

## Build (reproducible)
The shipped `native/prebuilt/Vortex_FcsCore.dll` (embedded in `Vortex_FS20_FCS.dll`) is built by `native/build-repro.sh`:
a plain `cargo build --release --locked --target x86_64-pc-windows-gnu` with the toolchain pinned in
`fcs_core/rust-toolchain.toml` (Rust 1.97.0), the build paths remapped and no linker timestamp, so the same source gives
the same bytes. The reference environment is the Docker image CI uses (Debian bookworm's mingw-w64 links):
```
docker run --rm -v "$PWD":/src -w /src rust:1.97.0-bookworm bash -c \
  "apt-get update -qq && apt-get install -y -qq gcc-mingw-w64-x86-64 >/dev/null && bash native/build-repro.sh"
```
It prints the SHA-256 of the DLL it built and of the committed prebuilt; they must be equal.

CI (`.github/workflows/native-core.yml`) does this on every push: it builds the core twice in different folders (the two
must match), publishes a signed build-provenance attestation for the DLL, and fails if the committed prebuilt is not
that build. `bash native/build.sh linux` builds the bench's `.so` (not shipped).

## Verify a shipped core
The core inside a release is `Vortex_FcsCore.dll`, an embedded resource of `Vortex_FS20_FCS.dll` (ILSpy / dnSpy:
Resources > Vortex_FcsCore.dll > Save). The game also writes it to `BepInEx\cache\Vortex_FcsCore\<hash>\Vortex_FcsCore.dll`.
```
gh attestation verify Vortex_FcsCore.dll -R duythinh2022-ui/Advanced-Flight-Controls-System-for-FS-20-Vortex-Nuclear-Option
```
This shows the workflow run and the commit that built that exact file. The plugin calls only the seven exports
(`rev_fcs_abi`, `rev_fcs_sizes`, `rev_fcs_solve`, `rev_fcs_grid`, `rev_fcs_grid_size`, `rev_fcs_cpl`, `rev_fcs_cpl_size`),
declared in `Vortex_FS20.FCS/FcsNative.cs`. The source (`fcs_core/src/lib.rs`, `cpl.rs`) is numeric code only: no file,
network, process or thread code. The DLL's import table still lists such Windows functions because Rust's standard
library is linked in; nothing in the core calls them.

## Check on the bench (strict float arithmetic: must be bit-identical)
```
bash bench.sh suite nat                                          # default: solve + both kernels native
bash bench.sh suite chk NativeAllocatorCheck=2                   # every solve both ways, compared bit for bit
bash bench.sh suite cs NativeAllocator=0                         # all C#
bash bench.sh suite koff NativeSweep=false NativeTables=false    # solve native, kernels in C#
bash bench.sh suite nogrid SweepGridCache=false                  # the sweep without the geometry cache
python3 native/verify/csvcmp.py bench/out base <tag>             # every per-case CSV (timing columns aside) and table vs base
```
`FCS_NATIVE_STATS=<file>` makes each bench run append its counts (`native/verify/nsum.py` sums them).

## Check on the game's own Mono runtime (native/verify)
`host.c` (from the F-22E) runs a managed exe on the game's `mono-2.0-bdwgc.dll` with Unity's `-O=-float32`, under Wine.
`jetcheck.cs` loads this plugin there with `Vortex_FcsCore.dll` beside it: the library loaded by full path, ABI and structure sizes, the
kernels' self-test against the C# twin as the game's Mono compiles it (repeated 200 times). `multicheck.cs` does the same with
several FCS plugins in one process.
