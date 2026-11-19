#!/usr/bin/env python3
# Per-AoA-band lateral derivatives, game vs bench, for the Ifrit / Revoker eqerr outputs (columns now_<surface>, tvc, tvcd, tvcy,
# splitL/R). Reports L_beta, N_beta (per deg per 1e4 Pa), damping, effectors and the stability-axis kB = N cos a - L sin a.
# usage: derivs_generic.py <eqerr csv...> [--bands=..]
import sys, os, glob, numpy as np, pandas as pd, numpy.linalg as la
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import eqcore
args = [a for a in sys.argv[1:] if not a.startswith("--")]
opt = {a.split("=")[0][2:]: a.split("=", 1)[1] for a in sys.argv[1:] if a.startswith("--")}
bands = [float(x) for x in opt.get("bands", "-45,0,20,40,50,60,70").split(",")]
D = eqcore.load(args); D = D[np.isfinite(D.pdot_m) & np.isfinite(D.rdot_m) & (D.tas > 40)].copy()
qb = D.qbar / 1e4
cols = D.columns
def col(n): return D[n] if n in cols else pd.Series(np.zeros(len(D)), index=D.index)
terms = {"pq": D.p * qb, "rq": D.r * qb, "beta": D.beta * qb}
pairs = [("elevator", "dElev"), ("aileron", "dAil"), ("flap", "dFlap"), ("canard", "dCan")]
for base, nm in pairs:
    if f"now_{base}_R" in cols: terms[nm] = (col(f"now_{base}_R") - col(f"now_{base}_L")) * qb
if "now_rudder" in cols: terms["rud"] = col("now_rudder") * qb
if "now_rudder_L" in cols: terms["rud"] = (col("now_rudder_L") + col("now_rudder_R")) * qb
if "tvcd" in cols and col("tvcd").abs().max() > 0.1: terms["tvcRoll"] = D.tw * np.sin(np.deg2rad(col("tvcd")))
if "tvcy" in cols and col("tvcy").abs().max() > 0.1: terms["tvcYaw"] = D.tw * np.sin(np.deg2rad(col("tvcy")))
if "splitL" in cols and (col("splitL").abs().max() > 0.01): terms["dSplit"] = (col("splitR") - col("splitL")) * qb
names = list(terms)
print(f"# {len(D)} samples; regressors: {names}; game | bench | ratio, deg/s^2 per unit (x qbar/1e4)")
out = {}
for ax, meas, pred in [("roll", "pdot_m", "pdot"), ("yaw", "rdot_m", "rdot")]:
    print(f"\n==== {ax} ====")
    print(f"{'band':>10} {'n':>6} " + " ".join(f"{n:>21}" for n in names) + "   R2g  R2b  rmsG rmsB")
    for lo, hi in zip(bands[:-1], bands[1:]):
        m = (D.alpha >= lo) & (D.alpha < hi)
        if m.sum() < 200: continue
        X = np.column_stack([np.ones(m.sum())] + [terms[n][m].values for n in names])
        yg = D.loc[m, meas].values; yb = D.loc[m, pred].values
        bg = la.lstsq(X, yg, rcond=None)[0]; bb = la.lstsq(X, yb, rcond=None)[0]
        r2g = 1 - np.sum((yg - X @ bg) ** 2) / np.sum((yg - yg.mean()) ** 2); r2b = 1 - np.sum((yb - X @ bb) ** 2) / np.sum((yb - yb.mean()) ** 2)
        cells = [f"{bg[i+1]:6.2f}|{bb[i+1]:6.2f}|{(bg[i+1]/bb[i+1] if abs(bb[i+1])>1e-6 else float('nan')):5.2f}" for i in range(len(names))]
        print(f"{int(lo):4d}..{int(hi):3d} {m.sum():6d} " + " ".join(f"{c:>21}" for c in cells) + f"  {r2g:5.2f} {r2b:5.2f} {np.sqrt(np.mean((yg-X@bg)**2)):5.1f} {np.sqrt(np.mean((yb-X@bb)**2)):5.1f}")
        out[(ax, lo, hi)] = (bg[names.index("beta") + 1], bb[names.index("beta") + 1], D.loc[m, "alpha"].mean())
print("\n==== stability-axis stiffness kB = N_beta cos a - L_beta sin a (per deg per 1e4 Pa), game | bench ====")
for lo, hi in zip(bands[:-1], bands[1:]):
    if ("roll", lo, hi) not in out: continue
    Lg, Lb, am = out[("roll", lo, hi)]; Ng, Nb, _ = out[("yaw", lo, hi)]; ar = np.deg2rad(am)
    print(f"{int(lo):4d}..{int(hi):3d} (mean a {am:5.1f}): L_beta {Lg:6.2f}|{Lb:6.2f}  N_beta {Ng:6.2f}|{Nb:6.2f}  kB {Ng*np.cos(ar)-Lg*np.sin(ar):6.2f}|{Nb*np.cos(ar)-Lb*np.sin(ar):6.2f}")
