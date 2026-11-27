#!/usr/bin/env python3
# Per-AoA-band control / stability derivatives of the GAME (regression on the SG-differentiated logged rates) and of the
# BENCH (same regression on the eqerr prediction), per axis. Ratio game/bench per term shows which effector or airframe
# term the bench mispredicts and how it varies with AoA. Usage: derivs.py <eqcmp dump csv> [--bands=...]
import sys, numpy as np, pandas as pd, numpy.linalg as la
D = pd.read_csv(sys.argv[1])
opt = {a.split("=")[0][2:]: a.split("=")[1] for a in sys.argv[2:] if a.startswith("--")}
bands = [float(x) for x in opt.get("bands", "-45,0,10,20,30,45,90").split(",")]
D = D[np.isfinite(D.pdot_m) & np.isfinite(D.qdot_m) & np.isfinite(D.rdot_m)].copy()
qb = D.qbar / 1e4
rad = np.deg2rad
terms = {
 "roll": dict(pq=D.p * qb, dAil=(D.ailR - D.ailL) * qb, dFlap=(D.flapR - D.flapL) * qb, dStab=(D.stabR - D.stabL) * qb, beta=D.beta * qb, rq=D.r * qb, dRud=(D.rudL + D.rudR) * qb),
 "pitch": dict(qq=D.q * qb, sStab=(D.stabR + D.stabL) * qb, sFlap=(D.flapR + D.flapL) * qb, sAil=(D.ailR + D.ailL) * qb, alpha=D.alpha * qb, Tsin=D.tw * np.sin(rad(D.tvc)), T=D.tw, hl=D.hl * qb, ab=D.ab * qb),
 "yaw": dict(rq=D.r * qb, dRud=(D.rudL + D.rudR) * qb, beta=D.beta * qb, pq=D.p * qb, dAil=(D.ailR - D.ailL) * qb, dFlap=(D.flapR - D.flapL) * qb, dStab=(D.stabR - D.stabL) * qb),
}
meas = {"roll": "pdot_m", "pitch": "qdot_m", "yaw": "rdot_m"}
pred = {"roll": "pdot_s", "pitch": "qdot_s", "yaw": "rdot_s"}
for ax in ["roll", "pitch", "yaw"]:
    print(f"\n==== {ax}: per AoA band, derivative (deg/s^2 per unit regressor), game | bench | ratio ====")
    names = list(terms[ax].keys())
    hdr = f"{'band':>10} {'n':>6} " + " ".join(f"{n:>22}" for n in names) + "   R2g  R2b  rmsG  rmsB"
    print(hdr)
    for lo, hi in zip(bands[:-1], bands[1:]):
        m = (D.alpha >= lo) & (D.alpha < hi)
        if m.sum() < 200: continue
        X = np.column_stack([np.ones(m.sum())] + [terms[ax][n][m].values for n in names])
        yg = D.loc[m, meas[ax]].values; yb = D.loc[m, pred[ax]].values
        bg = la.lstsq(X, yg, rcond=None)[0]; bb = la.lstsq(X, yb, rcond=None)[0]
        r2g = 1 - np.sum((yg - X @ bg) ** 2) / np.sum((yg - yg.mean()) ** 2)
        r2b = 1 - np.sum((yb - X @ bb) ** 2) / np.sum((yb - yb.mean()) ** 2)
        cells = []
        for i, n in enumerate(names):
            g, b = bg[i + 1], bb[i + 1]
            ratio = g / b if abs(b) > 1e-6 else float('nan')
            cells.append(f"{g:7.2f}|{b:7.2f}|{ratio:5.2f}")
        print(f"{int(lo):4d}..{int(hi):3d} {m.sum():6d} " + " ".join(f"{c:>22}" for c in cells) + f"  {r2g:5.2f} {r2b:5.2f} {np.sqrt(np.mean((yg-X@bg)**2)):5.1f} {np.sqrt(np.mean((yb-X@bb)**2)):5.1f}")
