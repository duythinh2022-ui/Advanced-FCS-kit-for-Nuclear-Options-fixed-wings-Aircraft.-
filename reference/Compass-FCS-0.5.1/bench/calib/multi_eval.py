#!/usr/bin/env python3
# per-log settings: multi_eval.py <bench dir> <json> <prefix> <tag> "<id>:<Name=val,...>" ... [--common=Name=val,...]
import sys, os, subprocess, json
sys.path.insert(0, os.path.dirname(__file__)); import eqcore
B, DATA, PFX, TAG = sys.argv[1:5]; specs = [a for a in sys.argv[5:] if not a.startswith("--")]
common = [x for a in sys.argv[5:] if a.startswith("--common=") for x in a[9:].split(",") if x]
LOGDIR = os.environ.get("FCS_LOGDIR", "/mnt/user-data/uploads/Nuclear Option/BepInEx"); OUT = f"{os.environ.get('EQ_OUT', 'eq')}/{TAG}"; os.makedirs(OUT, exist_ok=True)
env = dict(os.environ, BENCH_DATA=DATA); files = []
for sp in specs:
    i, _, rest = sp.partition(":"); sets = [x for x in rest.split(",") if x] + common
    out = f"{OUT}/{i}.csv"
    r = subprocess.run(["dotnet", f"{B}/bin/bench.dll", "eqerr", f"log={LOGDIR}/{PFX}{i}.csv", f"out={out}"] + sets, env=env, capture_output=True, text=True)
    if r.returncode: print(i, "FAILED", r.stderr[-300:]); continue
    files.append(out)
    s = eqcore.score([out]); print(f"  {i}: " + " ".join(f"{k} {v['rms']:.2f}" for k, v in s.items()))
s = eqcore.score(files)
print(TAG, " ".join(f"{k}: rms {v['rms']:.2f} bias {v['bias']:+.2f} scale {v['scale']:.3f} sd {v['sd']:.2f}" for k, v in s.items()))
json.dump(s, open(f"{OUT}/score.json", "w"))
