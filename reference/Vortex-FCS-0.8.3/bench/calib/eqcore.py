# shared scoring for eqerr outputs: SG-differentiated logged rates vs predicted accel (lag 0), spike-filtered
import numpy as np, pandas as pd
from scipy.signal import savgol_filter
WIN, POLY = 9, 3

def segments(t, dtmax=0.05):
    br = np.where(np.diff(t) > dtmax)[0] + 1
    return np.split(np.arange(len(t)), br)

def deriv(t, x):
    out = np.full(len(x), np.nan)
    for seg in segments(t):
        if len(seg) < WIN + 2: continue
        dt = np.median(np.diff(t[seg]))
        out[seg] = savgol_filter(x[seg], WIN, POLY, deriv=1, delta=dt)
    return out

def smooth(t, x):
    out = np.full(len(x), np.nan)
    for seg in segments(t):
        if len(seg) < WIN + 2: continue
        out[seg] = savgol_filter(x[seg], WIN, POLY)
    return out

def load(files):
    fr = []
    for f in files:
        d = pd.read_csv(f); d["src"] = f
        t = d["t"].values
        for ax in "pqr": d[ax + "dot_m"] = deriv(t, d[ax].values)
        d["nz_s"] = smooth(t, d["nz"].values)
        fr.append(d)
    D = pd.concat(fr, ignore_index=True)
    bad = (D["pdot_m"].abs() > 1500) | (D["qdot_m"].abs() > 800) | (D["rdot_m"].abs() > 400) | (D["nz"].diff().abs() > 1.5)
    for ax in "pqr": D.loc[bad, ax + "dot_m"] = np.nan
    D.loc[bad, "nz_s"] = np.nan
    return D

def score(files):
    D = load(files)
    out = {}
    for k, pr, me in [("p", "pdot", "pdot_m"), ("q", "qdot", "qdot_m"), ("r", "rdot", "rdot_m"), ("nz", "nzp", "nz_s")]:
        a = D[pr].values; b = D[me].values; m = np.isfinite(a) & np.isfinite(b)
        e = a[m] - b[m]
        out[k] = dict(rms=float(np.sqrt(np.mean(e ** 2))), bias=float(np.mean(e)), n=int(m.sum()), sd=float(np.std(b[m])), scale=float(np.sum(a[m] * b[m]) / np.sum(a[m] ** 2)))
    return out
