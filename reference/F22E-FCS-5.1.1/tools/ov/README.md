# Log-replay tools (26 Nov 2026, 4.8.15)
- `mkwin.py`: cuts the 10:34 log into one csv per no-TVC approach past 40 deg (57) -> win/, windows.json.
- `run.py <tag> <cg> [Name=value]`: replays all windows (bench replay) -> out/<tag>_<cg>/.
- `score.py <tag> <ref> <cg> [v]`: Tom's per-approach acceptance (overshoots fixed, others' peak / roll change).
- `curves.py <tag> <ref> <cg>`: pitch-rate curve quality (re-rise after max, jerk, chatter) while the stick is held.
- `fit.py <tags>`: replay match to the game (AoA, q, stab trim, peaks) - used for the stab-strength / CG fit.
- `check.py`, `sat.py`, `gap.py`, `anchor.py`: capability-model checks (plan vs achieved braking).
- `runseq.py`: sequential replay carrying learned state (option B; that code is not in 4.8.15).
- `bat.py`, `lomat.py`: scripted batteries (set BIN to the bench dir).
Bench world for this jet: tools/tzf.env + MassTarget=29200 FuelFrac=1 CgAftShift=0.0 MisStab=0.85 (best fit of the 10:34 log).
Paths are relative to this tree (see `_paths.py`; override with BENCH_DIR, RUNS, OV_DIR, FCS_LOGDIR). `tools/battery/` holds the newer copies of these scripts.
