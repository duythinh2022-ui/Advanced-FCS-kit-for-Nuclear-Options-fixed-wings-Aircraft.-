# Offline smoke test of the plugin DLL (Vortex FCS 0.5.2)

Runs what can run without the Unity player, under Mono (the game's runtime family; .NET 8 cannot start Harmony's
`AccessTools`, so it is no use for this):

1. **The vane box model's type** (`VaneBox`): its type initializer, then the mesh arrays `VaneBox.Build()` consumes
   (array lengths, index range, unit normals, finite values, `PairMesh` against the hinge list, the triangle count).
2. **Every type of the plugin**: its type initializer is run. A managed failure (NullReference, IndexOutOfRange, ...)
   would throw in the game too and fails the test. One that only says the Unity player is missing is listed, not counted.

```
bash tools/smoke/run.sh <path to Vortex_FS20_FCS.dll>      # exit code 0 = pass
```

Needs `mono`, the SDK's Roslyn (as `build.sh`) and the game's `NuclearOption_Data/Managed` folder complete (with only
the DLLs `build.sh` references, types that hold game types fail to load offline and the sweep reports them).

Why it exists: 0.5.1's vane box was never attached in game. `VaneBox.cs` had `static readonly Mesh[] vaneMesh = new
Mesh[VaneP.Length]`; `VaneP` is in the class's other file (`VaneBoxData.cs`), whose static initializers run after
`VaneBox.cs`'s (the compiler takes a partial class's files in the order it is given them: `build.sh` sorts by name), so
`VaneP` was null and the type initializer threw. On 0.5.1's DLL this test reports exactly that (`VaneBox:
NullReferenceException`, the one managed error among its 154 initializers); on 0.5.0's and 0.5.2's it passes.

What it does not do: nothing of Unity runs. `VaneBox.Attach` (meshes uploaded, renderers made, the LOD group joined) and
the drawing itself are only exercised in the game; the log line `Vortex FCS vane box model: ...` says how that went.
