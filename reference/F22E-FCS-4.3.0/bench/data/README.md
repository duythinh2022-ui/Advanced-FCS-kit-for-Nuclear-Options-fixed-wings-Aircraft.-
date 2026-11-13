# F-22E bench airframe data (not included)

The F-22E bench flies from two files that belong here:

- `airframe_cal.json`: Aryx's F-22E airframe (hierarchy, parts, masses, colliders, surfaces, engines, airfoils) with the
  2.4.18 calibration and the 3 Nov high-AoA (leaf) calibration applied;
- `f22e_damage.json`: per-part hit points, joints and engine damage data (damage suite).

They are extracted from Aryx's asset bundle (inside `Aryx_F22E_StrikeRaptor_1.0.4.dll`) and are not published with this kit.
Regenerate them with the scripts in `../extract/` (README §8.2.1) or ask the FCS author. Without them `bash bench.sh build`
still compiles, but `one` / `suite` / `replay` need `BENCH_DATA` to point at an airframe file.
