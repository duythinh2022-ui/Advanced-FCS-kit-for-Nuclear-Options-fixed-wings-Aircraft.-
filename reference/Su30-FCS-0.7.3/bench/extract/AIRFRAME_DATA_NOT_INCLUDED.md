# Airframe data not included

This FCS flies Su-30VXN Gandiva, which belongs to a third-party mod (kyleisred). Its airframe files come out of that work, so they are not
published with the kit.

Files the bench expects: `su30_airframe.json` and damage files.

Make them from your own copy of the game / mod with the extraction scripts in this folder (MANUAL §9.2), or run
`python fcs.py data <jet>` from the kit's root for the step-by-step. Without them the bench still **builds**;
`fly`, `suite` and `replay` need the files (or `BENCH_DATA` / `BENCH_DMG` pointing at them).
