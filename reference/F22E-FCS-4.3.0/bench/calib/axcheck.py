#!/usr/bin/env python3
# axial-force check: measured along-velocity acceleration (dTAS/dt + g sin(gamma), gamma from the logged attitude and alpha/beta)
# vs the eqerr prediction axp (thrust + aero, no gravity), per AoA band.  axcheck.py <eqerr csv> <log csv>
import sys, numpy as np, pandas as pd
from scipy.signal import savgol_filter
E = pd.read_csv(sys.argv[1]); L = pd.read_csv(sys.argv[2], usecols=["t", "tas", "alpha", "beta", "theta", "phi"], low_memory=False)
L = L.drop_duplicates("t").sort_values("t")
dt = np.median(np.diff(L.t)); L["dtas"] = savgol_filter(L.tas, 9, 3, deriv=1, delta=dt)
a = np.deg2rad(L.alpha); b = np.deg2rad(L.beta); th = np.deg2rad(L.theta); ph = np.deg2rad(L.phi)
# body velocity dir (x right, y up, z fwd); world up component after pitch theta (about x) and roll phi (about z): y_w = cos(th)*(cos(ph)*vy - sin(ph)*vx) + sin(th)*vz
vx, vy, vz = np.sin(b), -np.cos(b) * np.sin(a), np.cos(b) * np.cos(a)
for s in (1, -1):
    yw = np.cos(th) * (np.cos(ph) * vy - s * np.sin(ph) * vx) + np.sin(th) * vz
    L[f"ax{s}"] = L.dtas + 9.81 * yw
M = pd.merge_asof(E.sort_values("t"), L[["t", "ax1", "ax-1"]], on="t", direction="nearest", tolerance=0.01)
M = M[np.isfinite(M.ax1) & (M.tas > 40)]
best = "ax1" if np.sqrt(np.mean((M.axp - M.ax1) ** 2)) < np.sqrt(np.mean((M.axp - M["ax-1"]) ** 2)) else "ax-1"
M["axm"] = M[best]; M["e"] = M.axp - M.axm
print(f"# {len(M)} samples, roll sign {best}; axial accel RMS err {np.sqrt(np.mean(M.e**2)):.2f} m/s^2, bias {M.e.mean():+.2f}, spread of measured {M.axm.std():.2f}")
M["_b"] = pd.cut(M.alpha, [-60, -30, -20, -10, -5, 0, 5, 10, 15, 20, 30, 45, 60, 90])
print(M.groupby("_b", observed=True).agg(n=("e", "size"), tas=("tas", "mean"), ax_meas=("axm", "mean"), ax_pred=("axp", "mean"), bias=("e", "mean"), rms=("e", lambda x: np.sqrt(np.mean(x**2)))).round(2).to_string())
