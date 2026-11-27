# kit 5.0: where the battery / overlay scripts find things. Every path is relative to this F-22E tree; override with env vars.
import os
HERE = os.path.dirname(os.path.abspath(__file__))
TREE = os.path.dirname(os.path.dirname(HERE))                                   # reference/F22E-FCS-x.y.z
BENCH = os.environ.get("BENCH_DIR", os.path.join(TREE, "bench"))                # the bench (bench/bin/bench.dll after `bench build`)
RUNS = os.environ.get("RUNS", os.path.join(TREE, "runs"))                       # where results go
OV = os.environ.get("OV_DIR", os.path.join(TREE, "tools", "ov"))                # log windows (windows.json, win/, out/)
TZF = os.path.join(TREE, "tools", "tzf.env")                                             # the two-zone airframe settings every run uses
LOGDIR = os.environ.get("FCS_LOGDIR", os.path.join(os.environ.get("NO_GAME", "."), "BepInEx"))  # the game's FCS logs
DATA = os.environ.get("BENCH_DATA", os.path.join(BENCH, "data", "airframe_cal.json"))
os.environ.setdefault("BENCH_DATA", DATA)
