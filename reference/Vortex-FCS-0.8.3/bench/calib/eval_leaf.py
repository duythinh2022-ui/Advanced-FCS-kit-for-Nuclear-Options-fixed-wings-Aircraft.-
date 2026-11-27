#!/usr/bin/env python3
# evaluate parameter sets at the leaf: eval_leaf.py <bench> <json> <logdir> <ids> <tag> [--amin=40] [--every=3] [--finoff=..] Name=val ...
import sys, os, subprocess, numpy as np
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0, os.path.dirname(__file__)); import eqcore
B, DATA, LOGDIR, IDS, TAG = sys.argv[1:6]
opt = {a.split("=")[0][2:]: a.split("=", 1)[1] for a in sys.argv[6:] if a.startswith("--")}
sets = [a for a in sys.argv[6:] if not a.startswith("--")]
AMIN = float(opt.get("amin", 40)); every = opt.get("every", "3"); PFX = opt.get("prefix", "F22E_FCS_")
FINOFF = {k: float(v) for k, v in (x.split(":") for x in opt.get("finoff", "").split(",") if x)}
OUT = opt.get("out", f"{B}/out/ev_{TAG}"); os.makedirs(OUT, exist_ok=True); env = dict(os.environ, BENCH_DATA=DATA)
def one(i):
    out = f"{OUT}/{i}.csv"; subprocess.run(["dotnet", f"{B}/bin/bench.dll", "eqerr", f"log={LOGDIR}/{PFX}{i}.csv", f"out={out}", f"every={every}"] + sets, env=env, check=True, capture_output=True); return out
with ThreadPoolExecutor(2) as ex: files = list(ex.map(one, IDS.split(",")))
D = eqcore.load(files)
for f in files:
    i = os.path.basename(f)[:-4]
    if i in FINOFF: D = D[~((D.src == f) & (D.t >= FINOFF[i]))]
def sc(lo, hi):
    d = D[(D.alpha.abs() >= lo) & (D.alpha.abs() < hi)]; o = []
    for k, pr, me in [("p", "pdot", "pdot_m"), ("q", "qdot", "qdot_m"), ("r", "rdot", "rdot_m"), ("nz", "nzp", "nz_s")]:
        a = d[pr].values; b = d[me].values; m = np.isfinite(a) & np.isfinite(b); e = a[m] - b[m]; o.append(f"{k} {np.sqrt(np.mean(e**2)):.2f}({np.mean(e):+.1f})")
    return " ".join(o)
print(f"{TAG:28s} {' '.join(sets):40s} | >={AMIN:.0f}: {sc(AMIN,90)} | 40-55: {sc(40,55)} | 55-70: {sc(55,70)} | <35: {sc(0,35)}")
