#!/usr/bin/env python3
# Leaf calibration (3 Nov 2026): fit the bench-only high-AoA multipliers (Airframe.Mis*Hi, blended in over MisHiA0..MisHiA1)
# to the equation error of the training logs restricted to |alpha| >= amin, validate on held-out logs, and report the
# <35 deg error too (it must not move: the Hi multipliers are inactive there by construction).
# Usage: fit_leaf.py <bench dir> <airframe json> <logdir> <train ids csv> <val ids csv> [--params=a,b] [--x0=..] [--amin=40]
#        [--every=2] [--fixed=Name=val,...] [--maxfev=120] [--out=dir] [--finoff=id:t,...]
import sys, os, subprocess, numpy as np, pandas as pd, json, time
from scipy.optimize import minimize
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0, os.path.dirname(__file__)); import eqcore
B, DATA, LOGDIR, TRAIN, VAL = sys.argv[1:6]
opt = {a.split("=")[0][2:]: a.split("=", 1)[1] for a in sys.argv[6:] if a.startswith("--")}
params = opt.get("params", "MisFinHi,MisRudHi,MisWingHi,MisBodyHi").split(",")
x0 = [float(v) for v in opt.get("x0", ",".join(["1"] * len(params))).split(",")]
every = opt.get("every", "2"); AMIN = float(opt.get("amin", 40))
fixed = [f for f in opt.get("fixed", "").split(",") if f]
OUT = opt.get("out", "/home/claude/work/eq/leaf/fit"); os.makedirs(OUT, exist_ok=True)
FINOFF = {k: float(v) for k, v in (x.split(":") for x in opt.get("finoff", "").split(",") if x)}
env = dict(os.environ, BENCH_DATA=DATA)
NPAR = int(opt.get("jobs", "2"))

def run_one(i, sets, tag):
    log = f"{LOGDIR}/F22E_FCS_{i}.csv"; out = f"{OUT}/{tag}_{i}.csv"
    subprocess.run(["dotnet", f"{B}/bin/bench.dll", "eqerr", f"log={log}", f"out={out}", f"every={every}"] + sets, env=env, check=True, capture_output=True)
    return out

def score(files, lo, hi):
    D = eqcore.load(files)
    for f in files:
        i = os.path.basename(f).split("_", 1)[1][:-4]
        if i in FINOFF: D = D[~((D.src == f) & (D.t >= FINOFF[i]))]
    D = D[(D.alpha.abs() >= lo) & (D.alpha.abs() < hi)]
    out = {}
    for k, pr, me in [("p", "pdot", "pdot_m"), ("q", "qdot", "qdot_m"), ("r", "rdot", "rdot_m"), ("nz", "nzp", "nz_s")]:
        a = D[pr].values; b = D[me].values; m = np.isfinite(a) & np.isfinite(b); e = a[m] - b[m]
        out[k] = dict(rms=float(np.sqrt(np.mean(e ** 2))), bias=float(np.mean(e)), n=int(m.sum()))
    return out

def run(ids, x, tag):
    sets = [f"{p}={v:.5f}" for p, v in zip(params, x)] + fixed
    with ThreadPoolExecutor(NPAR) as ex: files = list(ex.map(lambda i: run_one(i, sets, tag), ids.split(",")))
    return score(files, AMIN, 90), score(files, 0, 35)

W = dict(p=1 / 95.5, q=1 / 38.8, r=1 / 22.5, nz=1 / 2.28)
def J_of(s): return sum(W[k] * s[k]["rms"] for k in W)
fmt = lambda s: " ".join(f"{k} {s[k]['rms']:.2f}" for k in ["p", "q", "r", "nz"])
hist = []
def obj(x):
    s, s_lo = run(TRAIN, x, "tr"); J = J_of(s); hist.append((J, list(x)))
    print(f"J {J:.4f}  " + " ".join(f"{p}={v:.3f}" for p, v in zip(params, x)) + "  | hi: " + fmt(s) + "  | lo: " + fmt(s_lo), flush=True)
    return J

t0 = time.time()
s0, s0lo = run(TRAIN, x0, "tr"); print("train before  |a|>=%g:" % AMIN, fmt(s0), " | <35:", fmt(s0lo), flush=True)
v0, v0lo = run(VAL, x0, "va"); print("val   before  |a|>=%g:" % AMIN, fmt(v0), " | <35:", fmt(v0lo), flush=True)
res = minimize(obj, x0, method="Nelder-Mead", options=dict(xatol=0.005, fatol=2e-4, maxfev=int(opt.get("maxfev", 120)), initial_simplex=np.array([x0] + [[v + (0.15 if j == i else 0) for j, v in enumerate(x0)] for i in range(len(x0))])))
x = res.x
s1, s1lo = run(TRAIN, x, "tr"); v1, v1lo = run(VAL, x, "va")
print("\nFIT:", " ".join(f"{p}={v:.3f}" for p, v in zip(params, x)), f"({len(hist)} evals, {time.time()-t0:.0f} s)")
print("train before:", fmt(s0), "| after:", fmt(s1), "   (<35 before/after:", fmt(s0lo), "/", fmt(s1lo), ")")
print("val   before:", fmt(v0), "| after:", fmt(v1), "   (<35 before/after:", fmt(v0lo), "/", fmt(v1lo), ")")
json.dump(dict(params=params, x=list(map(float, x)), fixed=fixed, amin=AMIN, train_before=s0, train_after=s1, val_before=v0, val_after=v1, train_lo_before=s0lo, train_lo_after=s1lo, val_lo_before=v0lo, val_lo_after=v1lo), open(f"{OUT}/result.json", "w"), indent=1)
