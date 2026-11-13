# rpcmp.py <replay.csv...> : phase-2 replay vs game, 4 s windows: AoA, beta, p, r, bank, flight-path roll rate, cap
import sys, numpy as np, pandas as pd
g = pd.read_csv("cal/f4.csv", low_memory=False)
def vv(d): a = np.deg2rad(d.alpha); return d.p * np.cos(a) + d.r * np.sin(a)
def row(d, t0, t1):
    x = d[(d.t >= t0) & (d.t < t1)]
    return f"a {x.alpha.mean():5.1f} b {x.beta.mean():+5.1f}/{x.beta.min():+5.1f} p {x.p.mean():+6.1f} r {x.r.mean():+6.1f} vv {vv(x).mean():+6.1f} phi {x.phi.iloc[-1]:+6.1f} sx {x.sr.mean():+5.2f}" if len(x) else ""
files = sys.argv[1:]
for t0 in range(993, 1023, 3):
    print(f"{t0:5d} GAME {row(g, t0, t0 + 3)}")
    for f in files:
        d = pd.read_csv(f); print(f"      {f.split('/')[-1][:-4]:>4} {row(d, t0, t0 + 3)}")
