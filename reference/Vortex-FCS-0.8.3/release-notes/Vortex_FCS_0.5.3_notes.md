# Vortex FCS 0.5.3: the vane box frame is matte

18 Nov 2026. On 0.5.2.

**Status: compiled and smoke-tested offline; the new look has not been seen in game.** One change, in `VaneBox.cs`. The control law, the mesh data and the config are 0.5.2's.

Tom, 18 Nov, first look at the model in game:

> the material looks too shiny, so the rim stands out more

Your screenshot is also the first confirmation that 0.5.2's fix worked: the box is attached and drawn, doors open, at 26 kt.

## Why it was shiny

The frame used the fan part's `aircraft_structure` material unchanged. That material is a shader graph (`Shader Graphs/Lit_vertexcolor`). Its only control over shine is a texture, `aircraft_structure_m` (R = metallic, A = smoothness); there is no smoothness slider to turn down.

Measured on that texture where the frame samples it, against the painted skin around the bay:

| | Metallic | Smoothness |
|---|---|---|
| Frame walls (stiffener and lightening-hole regions) | 0.53–0.55 | 0.35–0.48 |
| Rim and shelves (plain-sheet region) | 0.67 | 0.59 |
| Painted skin round the bay | 0.03 | about 0.19 (texture × the skin's 0.5) |

So the rim was polished metal next to matte paint. It catches the sky and reads lighter than it is.

## What changed

- The frame now draws with a copy of `aircraft_structure` whose metallic-smoothness texture is replaced by a flat one: **metallic 0.55, smoothness 0.20**.
- Colour, normal map, ambient occlusion and the vertex-colour tint are the original's. Only the shine changes. The fine variation in shine across the texture is gone (the colour and normal detail stay).
- The vanes are unchanged. They use `jet_mechanical`, the same patch as the fan's own stators.
- One copy per source material, shared by every Vortex. If the game swaps the fan part's material, the frame follows, as before.
- The attach log line ends with the frame's material: `frame material matte (metallic 0.55, smoothness 0.2)`. If the shader ever loses its `_MetallicSmoothness` texture (a game update), the frame falls back to the original material and the log says so.

## Not verified

- **The look in game.** My offline render uses base textures only, not the game's shading, so it can't judge shine.
- **The texture's channel use.** I took R = metallic, A = smoothness from the texture's name and Unity's usual layout; the shader graph's compiled code wasn't read. If the graph used G or B for smoothness instead, those are set to the same 0.55 byte value, which samples as about 0.26 through sRGB: still matte.
- Whether 0.20 is the right level. If the rim still stands out, the next lever is its colour: the rim and walls are tinted white like the bay doors' inner structure.

## Not built

Manual nozzle on the arrow keys in Stability Assist off was discussed and dropped (Tom: "get rid of the manual nozzle thing and keep it as it is"). Stability Assist off is unchanged.

## Checks

- Offline smoke test under Mono: all 154 type initializers OK, mesh data consistent (1308 triangles).
- Source zip rebuilds the DLL byte for byte.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.3 and `meta.json`; 0.5.2 kept as `Vortex_FS20_FCS.dll.0.5.2.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.3-release`.
- Restart the game to load it.
