# Airframe data not included

This jet is a third-party aircraft mod, so its extracted airframe file (and its damage file) is not published with the kit:

- Ternion FS-3: built in Blueprinter from someone else's blueprint (`ternion_airframe.json`, `../data/ternion_damage.json`).
- Su-30 (VXN): kyleisred's mod (`su30_airframe.json`, `su30_101_airframe.json`; its bench borrows `ternion_damage.json`).

The extraction scripts are here (`extract_ternion.py`, `extract_su30.py`, `build_json.py`, `defs.py`), so you can regenerate the files from your
own copy of the mod, or ask the FCS author. Without them `bash bench.sh build` still compiles; `fly` / `suite` / `replay` need `BENCH_DATA`
(and `BENCH_DMG` for the damage suite) pointing at the files. The Compass and Revoker files some of its bench modes compare against are stock-game data and are not included either
(see the Compass and Revoker trees' notes).
