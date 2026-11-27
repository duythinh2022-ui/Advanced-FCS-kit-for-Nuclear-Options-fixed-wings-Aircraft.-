# Airframe data not included

This jet is a stock Nuclear Option aircraft (KR-67 Ifrit). Its airframe and damage files are extracted from the game's own
assets, which belong to the game's developer, so they are not published with the kit.

Files the bench expects: `ifrit_airframe.json`, `../data/ifrit_damage.json`.

Make them from your own copy of the game / mod with the extraction scripts in this folder (MANUAL §9.2), or run
`python fcs.py data <jet>` from the kit's root for the step-by-step. Without them the bench still **builds**;
`fly`, `suite` and `replay` need the files (or `BENCH_DATA` / `BENCH_DMG` pointing at them).
