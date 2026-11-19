# derivs.py <tag> [--src=..] [--bands=..] [--excl=0] : per-AoA-band lateral derivatives, game | bench | ratio (deg/s^2 per unit x qbar/1e4)
import sys, numpy as np, pandas as pd, numpy.linalg as la
tag = sys.argv[1]; opt = {a.split("=")[0][2:]: a.split("=", 1)[1] for a in sys.argv[2:] if a.startswith("--")}
D = pd.read_pickle(f"/home/claude/tern/cal/{tag}.pkl")
if opt.get("excl", "1") == "1": D = D[~D.excl]
if "src" in opt: D = D[D.src.str.contains(opt["src"])]
if "t0" in opt: D = D[(D.t >= float(opt["t0"])) & (D.t <= float(opt["t1"]))]
D = D[np.isfinite(D.pdot_m) & np.isfinite(D.rdot_m)].copy()
bands = [float(x) for x in opt.get("bands", "-45,0,15,30,40,50,60,75").split(",")]
qb = D.qbar / 1e4
T = {"p": D.p * qb / D.tas * 100, "r": D.r * qb / D.tas * 100, "beta": D.beta * qb}
for s in ["elevator", "canard", "flaperon", "rollervon"]: T["d" + s[:4]] = (D[f"now_{s}_R"] - D[f"now_{s}_L"]) * qb
T["rud"] = (D.now_rudder_L + D.now_rudder_R) * qb
names = list(T)
print(f"# {tag}: {len(D)} samples; p, r terms x qbar/1e4 x 100/V")
res = {}
for ax, me, pr in [("roll", "pdot_m", "pdot"), ("yaw", "rdot_m", "rdot")]:
    print(f"\n==== {ax} ====\n{'band':>9} {'n':>5} " + " ".join(f"{n:>20}" for n in names) + "  rmsG rmsB  r2g")
    for lo, hi in zip(bands[:-1], bands[1:]):
        m = ((D.alpha >= lo) & (D.alpha < hi)).values
        if m.sum() < 150: continue
        X = np.column_stack([np.ones(m.sum())] + [T[n][m].values for n in names])
        yg = D[me].values[m]; yb = D[pr].values[m]
        bg = la.lstsq(X, yg, rcond=None)[0]; bb = la.lstsq(X, yb, rcond=None)[0]
        r2 = 1 - np.sum((yg - X @ bg) ** 2) / np.sum((yg - yg.mean()) ** 2)
        c = [f"{bg[i+1]:6.2f}|{bb[i+1]:6.2f}|{bg[i+1]/bb[i+1] if abs(bb[i+1])>1e-3 else np.nan:5.2f}" for i in range(len(names))]
        print(f"{int(lo):4d}..{int(hi):3d} {m.sum():5d} " + " ".join(f"{x:>20}" for x in c) + f" {np.sqrt(np.mean((yg-X@bg)**2)):5.1f} {np.sqrt(np.mean((yb-X@bb)**2)):5.1f} {r2:5.2f}")
        res[(ax, lo)] = (bg, bb, D.alpha.values[m].mean())
print("\n==== stability-axis kB = Nb cos a - Lb sin a, and |beta| range ====")
for lo, hi in zip(bands[:-1], bands[1:]):
    if ("roll", lo) not in res: continue
    (Lg, Lb, am) = (res[("roll", lo)][0][3], res[("roll", lo)][1][3], res[("roll", lo)][2]); Ng, Nb = res[("yaw", lo)][0][3], res[("yaw", lo)][1][3]
    a = np.deg2rad(am); m = (D.alpha >= lo) & (D.alpha < hi)
    print(f"{int(lo):4d}..{int(hi):3d} a {am:5.1f}: Lb {Lg:6.2f}|{Lb:6.2f}  Nb {Ng:6.2f}|{Nb:6.2f}  kB {Ng*np.cos(a)-Lg*np.sin(a):6.2f}|{Nb*np.cos(a)-Lb*np.sin(a):6.2f}  beta p5/p95 {np.percentile(D.beta[m],5):+.1f}/{np.percentile(D.beta[m],95):+.1f}")
