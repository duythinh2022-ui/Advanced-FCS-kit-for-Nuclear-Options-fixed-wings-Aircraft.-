#!/usr/bin/env python3
# Fit the bench's few named factors to the equation-error of the training logs (Nelder-Mead), validate on held-out logs.
# Usage: fit.py <bench dir> <airframe json> <log prefix> <train ids csv> <val ids csv> [--params=a,b,..] [--x0=..] [--every=2] [--fixed=Name=val,...]
import sys, os, subprocess, numpy as np, pandas as pd, json, time
from scipy.optimize import minimize
sys.path.insert(0, os.path.dirname(__file__))
B, DATA, PFX, TRAIN, VAL = sys.argv[1:6]
opt = {a.split("=")[0][2:]: a.split("=", 1)[1] for a in sys.argv[6:] if a.startswith("--")}
params = opt.get("params", "MisWing,MisBody,MisStab,IScaleRoll,IScalePitch,IScaleYaw,MisRud").split(",")
x0 = [float(v) for v in opt.get("x0", ",".join(["1"] * len(params))).split(",")]
every = opt.get("every", "2")
fixed = [f for f in opt.get("fixed", "").split(",") if f]
perlog = {kv.split(":")[0]: kv.split(":")[1].split(",") for kv in opt.get("perlog", "").split(";") if kv}
LOGDIR = os.environ.get("FCS_LOGDIR", "/mnt/user-data/uploads/Nuclear Option/BepInEx")
OUT = f"{os.environ.get('EQ_OUT', 'eq')}/fit_{PFX.strip('_')}"
os.makedirs(OUT, exist_ok=True)
env = dict(os.environ, BENCH_DATA=DATA)
import importlib.util
spec = importlib.util.spec_from_file_location("eqcore", os.path.join(os.path.dirname(__file__), "eqcore.py")); eqcore = importlib.util.module_from_spec(spec); spec.loader.exec_module(eqcore)

def run(ids, x, tag):
    sets = [f"{p}={v:.5f}" for p, v in zip(params, x)] + fixed
    files = []
    for i in ids.split(","):
        log = f"{LOGDIR}/{PFX}{i}.csv"; out = f"{OUT}/{tag}_{i}.csv"
        subprocess.run(["dotnet", f"{B}/bin/bench.dll", "eqerr", f"log={log}", f"out={out}", f"every={every}"] + sets + perlog.get(i, []), env=env, check=True, capture_output=True)
        files.append(out)
    return eqcore.score(files)

W = dict(p=1 / float(opt.get("wp", 95.5)), q=1 / float(opt.get("wq", 38.8)), r=1 / float(opt.get("wr", 22.5)), nz=1 / float(opt.get("wnz", 2.28)))
hist = []
def obj(x):
    s = run(TRAIN, x, "tr")
    J = sum(W[k] * s[k]["rms"] for k in W)
    hist.append((J, list(x), s))
    print(f"J {J:.4f}  " + " ".join(f"{p}={v:.3f}" for p, v in zip(params, x)) + "  | " + " ".join(f"{k} {s[k]['rms']:.2f}" for k in ["p", "q", "r", "nz"]), flush=True)
    return J

t0 = time.time()
s0 = run(TRAIN, x0, "tr"); print("train before:", {k: round(s0[k]["rms"], 3) for k in s0}, flush=True)
v0 = run(VAL, x0, "va"); print("val   before:", {k: round(v0[k]["rms"], 3) for k in v0}, flush=True)
res = minimize(obj, x0, method="Nelder-Mead", options=dict(xatol=0.004, fatol=1e-4, maxfev=int(opt.get("maxfev", 160)), initial_simplex=np.array([x0] + [[v + (0.08 if j == i else 0) for j, v in enumerate(x0)] for i in range(len(x0))])))
x = res.x
s1 = run(TRAIN, x, "tr"); v1 = run(VAL, x, "va")
print("\nFIT:", " ".join(f"{p}={v:.3f}" for p, v in zip(params, x)), f"({len(hist)} evals, {time.time()-t0:.0f} s)")
print("train before:", {k: round(s0[k]["rms"], 3) for k in s0}); print("train after: ", {k: round(s1[k]["rms"], 3) for k in s1})
print("val   before:", {k: round(v0[k]["rms"], 3) for k in v0}); print("val   after: ", {k: round(v1[k]["rms"], 3) for k in v1})
json.dump(dict(params=params, x=list(map(float, x)), train_before=s0, train_after=s1, val_before=v0, val_after=v1, fixed=fixed), open(f"{OUT}/result.json", "w"), indent=1)
