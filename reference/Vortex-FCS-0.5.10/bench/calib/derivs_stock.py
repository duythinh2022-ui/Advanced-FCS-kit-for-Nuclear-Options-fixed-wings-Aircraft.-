#!/usr/bin/env python3
# per-AoA-band control / stability derivatives, game vs bench, for the Ifrit / Revoker eqerr dumps (columns now_<surface>, splitL/R, tvc, tvcd, tvcy)
import sys, numpy as np, pandas as pd, numpy.linalg as la
D = pd.read_csv(sys.argv[1]); jet = sys.argv[2]
bands = [float(x) for x in (sys.argv[3] if len(sys.argv) > 3 else "-45,0,10,20,30,45,90").split(",")]
for c in ["pdot_s", "qdot_s", "rdot_s", "pdot_m", "qdot_m", "rdot_m"]: D = D[np.isfinite(D[c])]
qb = D.qbar / 1e4; rad = np.deg2rad
if jet == "ifrit":
    dEl = D.now_elevator_R - D.now_elevator_L; sEl = D.now_elevator_R + D.now_elevator_L; dAil = D.now_aileron_R - D.now_aileron_L; dFl = D.now_flap_R - D.now_flap_L; sFl = D.now_flap_R + D.now_flap_L; dSp = D.splitR - D.splitL; sSp = D.splitR + D.splitL
    terms = {"roll": dict(pq=D.p * qb, dAil=dAil * qb, dFlap=dFl * qb, dStab=dEl * qb, beta=D.beta * qb, rq=D.r * qb, tvcd=D.tw * np.sin(rad(D.tvcd)), dSplit=dSp * qb),
             "pitch": dict(qq=D.q * qb, sStab=sEl * qb, sFlap=sFl * qb, alpha=D.alpha * qb, Tsin=D.tw * np.sin(rad(D.tvc)), T=D.tw, hl=D.hl * qb, sSplit=sSp * qb),
             "yaw": dict(rq=D.r * qb, dStab=dEl * qb, beta=D.beta * qb, pq=D.p * qb, dAil=dAil * qb, dFlap=dFl * qb, dSplit=dSp * qb, tvcd=D.tw * np.sin(rad(D.tvcd)))}
else:
    dCan = D.now_canard_R - D.now_canard_L; sCan = D.now_canard_R + D.now_canard_L; dAil = D.now_aileron_R - D.now_aileron_L; sAil = D.now_aileron_R + D.now_aileron_L; rud = D.now_rudder
    terms = {"roll": dict(pq=D.p * qb, dAil=dAil * qb, dCan=dCan * qb, rud=rud * qb, beta=D.beta * qb, rq=D.r * qb, tvcy=D.tw * np.sin(rad(D.tvcy))),
             "pitch": dict(qq=D.q * qb, sCan=sCan * qb, sAil=sAil * qb, alpha=D.alpha * qb, Tsin=D.tw * np.sin(rad(D.tvc)), T=D.tw, hl=D.hl * qb, ab=D.ab * qb),
             "yaw": dict(rq=D.r * qb, rud=rud * qb, beta=D.beta * qb, pq=D.p * qb, dAil=dAil * qb, dCan=dCan * qb, tvcy=D.tw * np.sin(rad(D.tvcy)))}
meas = {"roll": "pdot_m", "pitch": "qdot_m", "yaw": "rdot_m"}; pred = {"roll": "pdot_s", "pitch": "qdot_s", "yaw": "rdot_s"}
for ax in terms:
    names = list(terms[ax]); print(f"\n==== {ax}: game | bench | ratio ====")
    for lo, hi in zip(bands[:-1], bands[1:]):
        m = (D.alpha >= lo) & (D.alpha < hi)
        if m.sum() < 120: continue
        X = np.column_stack([np.ones(m.sum())] + [terms[ax][n][m].values for n in names]); yg = D.loc[m, meas[ax]].values; yb = D.loc[m, pred[ax]].values
        bg = la.lstsq(X, yg, rcond=None)[0]; bb = la.lstsq(X, yb, rcond=None)[0]
        r2g = 1 - np.sum((yg - X @ bg) ** 2) / np.sum((yg - yg.mean()) ** 2)
        print(f"{int(lo):4d}..{int(hi):3d} n={m.sum():5d} R2g {r2g:.2f} " + " ".join(f"{n}:{bg[i+1]:6.2f}|{bb[i+1]:6.2f}|{(bg[i+1]/bb[i+1] if abs(bb[i+1])>1e-6 else float('nan')):4.2f}" for i, n in enumerate(names)))
