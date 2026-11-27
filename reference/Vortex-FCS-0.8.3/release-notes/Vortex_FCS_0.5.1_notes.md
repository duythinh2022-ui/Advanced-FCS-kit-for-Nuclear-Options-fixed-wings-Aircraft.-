# Vortex FCS 0.5.1: vane box redrawn

18 Nov 2026. On 0.5.0.

**Status: model only, compiled only, not seen in game.** The control law's source files are 0.5.0's, file for file; four files differ (the model's code, its mesh data, the version, one config description). 0.5.0's bench results stand and were not re-run. The new model was checked offline against the game's own meshes.

Tom, 18 Nov, on 0.5.0's preview:

> Thats ugly, why the middle is so thick, why is it just a square in a circle. Fix that

## Why 0.5.0 looked like that

- **Thick middle:** the fan's hub fairing (the cone under the fan's hub) hangs down the middle of the duct to 13 mm above the duct's exit. I put the vanes inside the duct, so the keel had to go round the cone: a channel 0.34 m wide.
- **Square in a circle:** I kept the whole box inside the round duct, so it was a rectangle inscribed in the circle with a dark shroud filling the rest.

## What 0.5.1 draws

Everything now hangs at the duct's exit and below it, clear of the cone (the vanes' top edges are level with its tip, the keel is under it).

- **Keel:** an 18 mm plate, 100 mm deep, across the whole exit.
- **Vanes:** eight pairs (was six), 90 mm chord, 140 mm apart. They run from the keel to the frame over the whole round exit; their tips are cut to follow the frame's round ends.
- **Frame:** a white skirt round the whole exit, hanging 75 mm below it, with flat sides at 0.48 m from the centre.
- **No shroud.** Vanes cover the exit except two slivers outboard of the flat sides (12 cm at their widest), closed by a plate in the duct's dark tone.
- Materials, LOD group, door switch and the way it follows the fan's thrust line are as 0.5.0. 1308 triangles in 9 renderers (0.5.0: 912 in 7).

## Why not a box wider than the duct, like the real one

I measured the bay under the exit before redrawing. There is almost no bay:

- The duct's exit is 5 to 12 cm above the closed doors' inner skin.
- Around the exit the bay's ceiling is a bevel that falls straight away to the door opening's edge.
- Outboard of 0.5 m from the centre the doors' skin rises (−1.23 m at 0.52, −1.19 m at 0.60), leaving no depth for a wall or a 9 cm vane. That is why the sides are flat.

So a rectangular box that fills the door opening would have to pass through the bay's ceiling and the closed doors. The round frame with flat sides is the largest that fits.

## Offline checks (exact mesh-to-mesh distances against the game's meshes)

| Check | Result |
|---|---|
| Vane ends to keel and to frame, whole travel | 10 mm |
| Vanes to the hub fairing's cone, whole travel | 26 mm or more |
| Vane to neighbouring vane, whole travel | 93 mm or more |
| Keel to the cone's tip | 14 mm |
| Frame / vanes / keel to the closed doors | 22 / 28 / 37 mm, more as the doors open |
| Frame's inner wall to the duct wall | 19 mm |
| Frame top to the fan blades | 430 mm |
| Coplanar overlapping faces (z-fighting), inside the model and against fan part, doors, intake and wing roots | 0 |
| Triangle winding against the game's convention | 1308 of 1308 |

Three parts end inside the airframe on purpose so no edge shows: the skirt's outer face above the exit (4 mm behind the duct wall), the shelves' rims (above the bay's bevel all the way round; that hidden rim is 15 mm from the closed doors' ribs on the other side of the bay's skin), and the pivot pins (inside the keel and the frame).

The two vane pairs next to the fan's axis pass close by the cone's tip; their inner top corners are cut back 30 mm, which is what gives the 26 mm.

## What only the game can show

Same list as 0.5.0: whether the structure shader tints by vertex colour as assumed (white frame and keel, dark shelves), the lighting, and the LOD group line in the log (`Vortex FCS vane box model: 8 vane pairs + frame on liftFan, 1308 triangles, ...`). The preview picture is a software render with base textures, not the game's shading.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.1 and `meta.json`; 0.5.0 kept as `Vortex_FS20_FCS.dll.0.5.0.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.1-release`. The source zip rebuilds the DLL byte for byte.
