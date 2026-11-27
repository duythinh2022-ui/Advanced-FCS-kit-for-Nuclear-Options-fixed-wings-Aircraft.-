# F-22E 5.x bench batteries

The scripts the F-22E 5.0–5.1 releases (pair allocator, horizon objective) were judged with. They came with the 5.1.1
release as its outer `tools/` folder. Kit 5.0 made every path relative to this tree (`_paths.py`), so they run from any
checkout once the bench is built (`python fcs.py bench f22e build` from the kit root, or `bash bench.sh build` here).

Paths you can override: `BENCH_DIR` (the bench), `RUNS` (results, default `<tree>/runs`), `OV_DIR` (log windows,
default `tools/ov`), `FCS_LOGDIR` (your game's `BepInEx` folder with the FCS logs), `BENCH_DATA` (the airframe file).
They need Python 3 with `pandas` and `numpy`.

| script | what it runs | usage |
|---|---|---|
| `bat.py` | the 25 Nov battery: roll reversal, slow roll, push-over with roll, pull, at several AoA caps, TVC off | `bat.py <tag> [Name=value ...]` |
| `cmpbat.py` | compares two battery tags per CG (roll rate, sideslip) | `cmpbat.py <tagA> <tagB> <battery>` |
| `prel.py` | chatter repro: push or pull, release, full roll held 4 s; counts stab command reversals | `prel.py <tag> [Name=value ...]` |
| `grel.py` | stuck-pose repro: full aft, release, full roll held 5 s (TVC off) | `grel.py <tag> [Name=value ...]` |
| `traps.py` | replays the four logged reverse-roll traps from the game state | `traps.py [Name=value ...]` (logs from `FCS_LOGDIR`) |
| `revsplit.py` | counts reversed stab splits and a chatter index in a run folder | `revsplit.py <run folder>` |
| `twitch.py` | surface command reversal counts per run | `twitch.py <tag> ...` |
| `lomat.py` | low-AoA roll-stop matrix (pitch stick 0–0.3, full roll, release) | `lomat.py <bin> <tag> [Name=value ...]` |
| `stop.sh` | one pitch-released full roll at 450 km/h, cap 36, no TVC | `stop.sh <tag> <roll start s> [Name=value ...]` |
| `mkwin.py` | cuts a flight log into windows (`tools/ov/windows.json`, `tools/ov/win/`) | `mkwin.py [log.csv]` |
| `run.py`, `runseq.py` | replays every window with a config (`runseq` carries the learned stores window to window) | `run.py <tag> <cg> [Name=value ...]` |
| `score.py`, `curves.py` | per-window acceptance against a reference tag, and pitch-rate curve quality | `score.py <tag> <ref> <cg>` |

`tzf.env` holds the two-zone airframe settings every run uses. The bench is relative: compare tags against each other,
not against flight numbers (MANUAL §17).
