# merge_f4.py <tag>: interpolate the fuel-trim bins on the logged lateral CG offset -> cal/<tag>_f4.pkl
import sys, numpy as np, pandas as pd
sys.path.insert(0, "/home/claude/tern/w/bench/calib"); import eqcore
tag = sys.argv[1]; O = f"/home/claude/tern/cal/{tag}"
S = [0, 600, 1200, 1800]
cg = [float(open(f"{O}/f4_s{s}.cg").read().split()[1]) for s in S]
runs = [pd.read_csv(f"{O}/f4_s{s}.csv") for s in S]
lc = pd.read_csv("/home/claude/tern/cal/f4_tw1.15.csv", low_memory=False)
extra = ["t", "massKg", "asymDy", "dMeasR", "dMeasY", "asymMr", "asymMy", "pCmd", "rCmd", "stickX", "asymCap", "asymU", "hlPos"]
extra = [c for c in extra if c in lc.columns]
D = runs[0].copy().merge(lc[extra].drop_duplicates("t"), on="t", how="left", suffixes=("", "_log"))
w = np.interp(D.asymDy.values, cg[::-1], np.arange(4)[::-1])
i0 = np.minimum(w.astype(int), 2); fr = w - i0
for k in ["pdot", "qdot", "rdot", "nzp", "nyp", "Mx", "My", "Mz"]:
    V = np.column_stack([r[k].values for r in runs]); D[k] = V[np.arange(len(D)), i0] * (1 - fr) + V[np.arange(len(D)), i0 + 1] * fr
tt = D.t.values
for ax in "pqr": D[ax + "dot_m"] = eqcore.deriv(tt, D[ax].values)
D["nz_s"] = eqcore.smooth(tt, D.nz.values); D["excl"] = False; D["src"] = "f4"
D.to_pickle(f"/home/claude/tern/cal/{tag}_f4.pkl")
def st(d, pr, me):
    a = d[pr].values; b = d[me].values; m = np.isfinite(a) & np.isfinite(b); e = a[m] - b[m]
    return f"{np.sqrt(np.mean(e**2)):6.2f} b{np.mean(e):+6.2f} sd{np.std(b[m]):5.1f} k{np.sum(a[m]*b[m])/np.sum(a[m]**2):4.2f}"
for nm, lo, hi in [("phase1", 961, 986), ("phase2", 992, 1024), ("p2 a>40", 992, 1024)]:
    d = D[(D.t >= lo) & (D.t <= hi)]
    if "a>40" in nm: d = d[d.alpha > 40]
    print(f"{nm:>8} n{len(d):5d}  roll {st(d,'pdot','pdot_m')}  pitch {st(d,'qdot','qdot_m')}  yaw {st(d,'rdot','rdot_m')}  nz {st(d,'nzp','nz_s')}")
