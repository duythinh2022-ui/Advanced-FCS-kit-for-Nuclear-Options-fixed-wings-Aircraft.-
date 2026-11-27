# Airframe data not included

This FCS flies J-20B Weilong, which belongs to a third-party mod (Reasonix). Its airframe files come out of that work, so they are not
published with the kit.

Files the bench expects: `j20_airframe.json` here, made by `j20_extract.py` from the mod's asset bundle and its AeroControl
runtime contract (the file's `"j20"` block is what lets the bench stand in for the mod's surface system; with any other
airframe file the FCS waits for that system and the run stops). The damage suite borrows the Revoker's `revoker_damage.json`.

Make them from your own copy of the game / mod with the extraction scripts in this folder (MANUAL §9.2), or run
`python fcs.py data <jet>` from the kit's root for the step-by-step. Without them the bench still **builds**;
`fly`, `suite` and `replay` need the files (or `BENCH_DATA` / `BENCH_DMG` pointing at them).
