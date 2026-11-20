# Vortex FCS 0.5.5: the vane box rim takes the bay's own paint

18 Nov 2026. On 0.5.4.

**Status: compiled and smoke-tested offline; not seen in game.** Model only: the control law, the throttle hold (0.5.4) and the config are unchanged.

Tom, 18 Nov (on 0.5.3):

> the rim color still doesnt match the yellow-ish white around it

## Why it didn't match

The light surround in your screenshot is the lift fan part's bevel: the funnel from the duct exit down to the bay edges. It isn't structure material. It is drawn with **`SmallFighter1_skin`**, the livery material, on its own strip of the skin texture. The rim used `aircraft_structure` (white tint), a different texture and shader, so no tint or shine setting on that material could match it.

Measured on the game's mesh: the four bevel patches around the exit (fore, aft, both sides) sit on the skin atlas row v 0.94–0.99. In the default livery they are 0.62–0.63 grey with AO 0.80–0.84. Your livery paints them the yellow-ish white you see.

## What changed

- The frame's outward faces now draw with the fan part's own skin material: the bottom rim, the outer walls below the exit, and the shelf undersides (102 of the frame's 220 triangles).
- They sample the skin texture at the same spot as the bevel: uv (0.85851, 0.97037), the most uniform point of that strip. Across 13×13 texels the colour and normal map vary by 0.011. Default livery there: 0.625 grey, AO 0.81, smoothness 0.23.
  - So the rim picks up whatever colour the livery gives the bay, whichever livery is on.
  - It does not pick up the bevel's panel detail: the whole rim maps onto a few texels.
- The inner walls (facing the vanes), tops and keel keep the matte structure from 0.5.3. The vanes are unchanged.
- **Material lookup:** the skin is found by name, or by its slot in the fan part's materials if a livery replaces it with a differently named one. It is re-checked twice a second like the other materials. If it can't be found, the rim falls back to the structure material, and the log line says which one the rim got (`rim SmallFighter1_skin (slot 0, the bay bevel's paint)`).
- **Geometry unchanged:** positions, normals, triangles and winding are identical to 0.5.3, and the vane meshes are identical. The clearance and z-fight results stand.
- **Cost:** one more draw call. The frame now has two materials, so the box is 10 draws in all; still 1308 triangles.

## Not verified

- The look in game. The rim is flat and the bevel is sloped (its normals point 30–45° outboard), so in some light the two may still shade a little differently even with the same paint.
- That your livery paints the bay uniformly around the sampled spot. If it has markings exactly there, the rim takes that colour. I can't see liveries offline; tell me if it's off and I'll move the sample point.

## Checks

- Smoke test under Mono: all 154 type initializers OK. Mesh data is consistent, and the frame splits into 118 structure + 102 skin triangles.
- Generator check: frame geometry and vane meshes are identical to 0.5.3, winding is right on 220 of 220 triangles, and every skin triangle has a non-zero UV area (needed for tangents).
- The source zip rebuilds the DLL byte for byte.

## Install

- `BepInEx\plugins\Vortex_FS20.FCS\Vortex_FS20_FCS.dll` 0.5.5 and `meta.json`; 0.5.4 kept as `Vortex_FS20_FCS.dll.0.5.4.bak`.
- Release folder: `C:\Games\NO mods\Vortex-FCS-0.5.5-release`.
- Restart the game to load it.
