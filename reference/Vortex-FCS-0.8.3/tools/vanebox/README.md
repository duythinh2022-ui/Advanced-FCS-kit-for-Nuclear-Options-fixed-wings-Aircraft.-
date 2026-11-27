# Vane box model tools (Vortex FCS 0.5.0, redrawn in 0.5.1)

How `Vortex_FS20.FCS/VaneBoxData.cs` was made and checked. The scripts are kept as they were run (paths under
`/home/claude/work/geo`); none of them contains game data. They read the game's own files, which are not in this zip.

| Script | What it does |
|---|---|
| `ex1.py`, `ex2.py`, `ex3.py` | Read the FS-20's hierarchy, mesh records and materials out of `resources.assets` (UnityPy). |
| `dec.py` | Decodes the vertex streams of those meshes from slices of `resources.assets.resS`. |
| `tex.py` | Decodes the three material atlases (DXT1 / DXT5) to PNG for choosing texture regions. |
| `scene.py` | Helpers: a part's mesh in the airframe frame, plane cuts, rotations. |
| `sect.py` | Section drawings of the fan bay (the measurements in `VaneBox.cs`'s header come from these and from `clear.py`). |
| `gen.py` | **The model** (0.5.1: the second design, under the duct's exit; 0.5.0's is in that release's source zip). All dimensions are the constants at its top. Builds the frame mesh (skirt with flat sides, shelves, keel) and the vane-pair meshes. |
| `clear.py` | Exact mesh-to-mesh distances (FCL) from the box and the vanes, over the vanes' whole travel, to the fan part, the blades and the doors through their travel. |
| `zfight.py` | Searches for coplanar overlapping faces that face the same way (z-fighting), inside the model and against the game's meshes. |
| `rast.py` | A small software renderer for preview pictures (base textures, lambert light, the game's back-face culling). Not the game's shading. |
| `export_cs.py` | Writes `VaneBoxData.cs` from `gen.py`. |

To change the model: edit the constants in `gen.py`, run `clear.py` and `zfight.py`, then `export_cs.py <path to VaneBoxData.cs>`.

Texture orientation: in the PNGs `tex.py` writes, row 0 is v = 0 (the image is upside down against UV space). Checked against
the game's own meshes: with that orientation every `jet_mechanical` triangle of the fan part samples opaque texels.
