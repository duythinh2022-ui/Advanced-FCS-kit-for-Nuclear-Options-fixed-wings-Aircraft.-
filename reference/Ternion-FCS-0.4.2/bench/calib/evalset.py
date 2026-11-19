#!/usr/bin/env python3
# evaluate a parameter set on a list of logs: evalset.py <bench dir> <json> <prefix> <ids> <tag> [Name=val ...]
import sys, os, subprocess, json
sys.path.insert(0, os.path.dirname(__file__)); import eqcore
B, DATA, PFX, IDS, TAG = sys.argv[1:6]; sets = sys.argv[6:]
LOGDIR = os.environ.get("FCS_LOGDIR", "/mnt/user-data/uploads/Nuclear Option/BepInEx"); OUT = f"{os.environ.get('EQ_OUT', 'eq')}/{TAG}"; os.makedirs(OUT, exist_ok=True)
env = dict(os.environ, BENCH_DATA=DATA); files = []
for i in IDS.split(","):
    out = f"{OUT}/{i}.csv"
    subprocess.run(["dotnet", f"{B}/bin/bench.dll", "eqerr", f"log={LOGDIR}/{PFX}{i}.csv", f"out={out}"] + sets, env=env, check=True, capture_output=True); files.append(out)
s = eqcore.score(files)
print(TAG, " ".join(f"{k}: rms {v['rms']:.2f} bias {v['bias']:+.2f} scale {v['scale']:.3f} sd {v['sd']:.2f}" for k, v in s.items()))
json.dump(s, open(f"{OUT}/score.json", "w"))
