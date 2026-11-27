# score.py <tag> [--bands] : equation-error RMS per axis, overall and by AoA band
import sys, numpy as np, pandas as pd
tag = sys.argv[1]; D = pd.read_pickle(f"/home/claude/tern/cal/{tag}.pkl"); D = D[~D.excl]
def st(d, pr, me):
    a = d[pr].values; b = d[me].values; m = np.isfinite(a) & np.isfinite(b); e = a[m] - b[m]
    return np.sqrt(np.mean(e**2)), np.mean(e), np.std(b[m]), np.sum(a[m]*b[m])/np.sum(a[m]**2), m.sum()
ax = [("roll", "pdot", "pdot_m"), ("pitch", "qdot", "qdot_m"), ("yaw", "rdot", "rdot_m"), ("nz", "nzp", "nz_s")]
print(f"{'set':>12} {'n':>6} " + " ".join(f"{a:>26}" for a, _, _ in ax))
def row(name, d):
    cells = []
    for a, pr, me in ax:
        r, b, s, k, n = st(d, pr, me); cells.append(f"{r:6.2f} b{b:+6.2f} sd{s:6.1f} k{k:4.2f}")
    print(f"{name:>12} {len(d):6d} " + " ".join(f"{c:>26}" for c in cells))
row("all", D)
for lo, hi in [(-60, -5), (-5, 10), (10, 20), (20, 30), (30, 40), (40, 50), (50, 60), (60, 90)]:
    d = D[(D.alpha >= lo) & (D.alpha < hi)]
    if len(d) > 300: row(f"{lo}..{hi}", d)
for s, d in D.groupby("src"): row(s[-6:], d)
