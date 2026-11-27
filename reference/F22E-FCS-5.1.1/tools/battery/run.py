# replay every window with a named config:  run.py <tag> <cg> [Name=value ...]   -> out/<tag>_<cg>/wNN.csv
import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *  # kit 5.0: paths relative to this tree
import json, subprocess, sys, os
from concurrent.futures import ThreadPoolExecutor
B=BENCH; BIN=os.environ.get("BENCHBIN", B+"/bin/bench.dll")
tag, cg = sys.argv[1], sys.argv[2]; extra = sys.argv[3:]
TZ = open(TZF).read().split()
base = TZ + ["MassTarget=29200", "FuelFrac=1", f"CgAftShift={cg}"]
W = json.load(open(OV+"/windows.json"))
only = os.environ.get("ONLY"); 
if only: W=[w for w in W if w['id'] in set(int(x) for x in only.split(','))]
od = f"{OV}/out/{tag}_{cg}"; os.makedirs(od, exist_ok=True)
env = dict(os.environ)
def one(w):
    o = f"{od}/w{w['id']:02d}.csv"
    r = subprocess.run(["dotnet", BIN, "replay", f"log={OV}/win/w{w['id']:02d}.csv", f"t0={w['t0']}", f"t1={w['t1']}", f"out={o}"] + base + extra, cwd=B, env=env, capture_output=True, text=True)
    if r.returncode: print("FAIL", w['id'], r.stderr[-400:])
with ThreadPoolExecutor(int(os.environ.get("J","2"))) as ex: list(ex.map(one, W))
