# Airframe data not included

This jet is a stock Nuclear Option aircraft. Its airframe and damage files are extracted from the game's own assets, which belong
to the game's developer, so they are not published with the kit:

- T/A-30 Compass: `compass_airframe.json`, `../data/compass_damage.json` (this bench also compares against the Revoker's `rev_airframe.json` and `../data/revoker_damage.json`).

Regenerate them from your own copy of the game with the extraction scripts in this folder (`dump.py`, `build_json.py`,
`defs.py`, `stock_damage.py`; README §9.2). Without them `bash bench.sh build` still compiles; `fly` / `suite` / `replay` need
`BENCH_DATA` (and `BENCH_DMG` for the damage suite) pointing at the files, or the files placed at the paths above.
