# Vortex FCS 0.5.2: the vane box model is attached again

18 Nov 2026. On 0.5.1.

**Status: compiled and smoke-tested offline; the model has still not been seen in game.** The control law, the mesh data and the config are 0.5.1's, file for file. Three source files differ: `VaneBox.cs`, `Registry.cs` and the version.

Tom, 18 Nov:

> i dont see it in game

## What happened

The model was never built in 0.5.1. Your game log (`BepInEx\LogOutput.log`, plugin 0.5.1 loaded) has this line twice, once per airframe set-up:

```
[Warning:FS-20 Vortex FCS] Vortex FCS vane box model: The type initializer for 'Vortex_FS20.FCS.VaneBox' threw an exception.
```

A type initializer is the code that sets a class's static fields the first time the class is used. If it throws, nothing of the class can run. So `VaneBox.Attach`, which builds the meshes and renderers, never started.

The cause is my mistake, introduced with 0.5.1's redraw:

- `VaneBox.cs` had `static readonly Mesh[] vaneMesh = new Mesh[VaneP.Length];`
- `VaneP` (the vane meshes' vertex data) is in the same class's other file, `VaneBoxData.cs`.
- A class split over two files has its static fields set file by file, in the order the compiler is given the files. `build.sh` sorts them by name, so `VaneBox.cs` goes first and `VaneP` was still null when its length was read: NullReferenceException.

0.5.0's code had no such initializer (one vane mesh, no array sized from the data file), so its type initializer was fine.

## Confirmed two ways

| Check | 0.5.0 | 0.5.1 | 0.5.2 |
|---|---|---|---|
| IL of the type initializer: `VaneP` read before it is assigned | no such read | yes (read at offset 0x32, assigned later) | no such read |
| DLL's type initializer run under Mono offline | ok | NullReferenceException in `VaneBox..cctor` at 0x32 | ok |
| All plugin types' initializers under Mono | 151 of 151 ok | 153 of 154 ok, 1 error (`VaneBox`) | 154 of 154 ok |
| Mesh arrays (lengths, index range, unit normals, finite values) | consistent | not reached | consistent, 1308 triangles |

Mono is the runtime family the game uses, so the offline run reproduces the game's error exactly. It does not run anything of Unity.

## What changed

- **Fix:** `vaneMesh` is made inside `Attach`. Nothing static in `VaneBox.cs` reads the data file's fields any more.
- **Log:** if attaching fails, the log now has the whole exception (type, cause, stack) under `Vortex FCS vane box model not attached:`. 0.5.1's line did not say what threw.
- **Log:** the success line now ends with the fan part's scale and whether the box is drawn at that moment. Expected on spawn: `Vortex FCS vane box model: 8 vane pairs + frame on liftFan, 1308 triangles, in the airframe's LOD group (level 0 of 2, with the fan part); part scale (1, 1, 1); hidden until the fan doors open`.
- **`tools/smoke`** (new, in the source zip): the offline test above. It fails on 0.5.1's DLL and passes on 0.5.0's and 0.5.2's. It is part of this mod's release checks now (`tools/smoke/README.md`).

## What was not affected

The law and the vane actuator do not use this class. Your 08:46 log on 0.5.1 has the vane angle the FCS applies (`stVaneNow`) running −10 to +37.9° with the doors open, so the FCS was driving the vanes while the model was missing.

## What is still unverified

- **`Attach` itself has never run in a game I have a log of.** It is Unity code (meshes uploaded, nine renderers made, the airframe's LOD group joined), and nothing offline can run it. What I re-checked against the game's own asset data: the fan thrust transform's parent is the part `liftFan`, its mesh has the 3720 vertices the model is sized on, its renderer carries the structure and mechanical materials, its scale is 1, and it sits in level 0 of the airframe's two-level LOD group.
- **The look** (vertex-colour tint of the structure shader, lighting) is as uncertain as in 0.5.1's notes.
- Whether 0.5.0's model was ever drawn in game is unknown: its initializer passes offline, and there is no game log from it.

## When to look

The box is drawn only while the fan doors are open (STOVL mode), in the near LOD level. With the doors closed it is switched off.

If it is still missing, the log line says why. I can read `LogOutput.log` from the connected game folder myself.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.2 and `meta.json`; 0.5.1 kept as `Vortex_FS20_FCS.dll.0.5.1.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.2-release`. The source zip rebuilds the DLL byte for byte.
- Restart the game to load it.
