#!/usr/bin/env python3
# Compare eqerr predictions with the logged rates: SG-differentiate p/q/r, align (lag scan), report RMS per axis and
# error maps. Usage: eqcmp.py <eqerr csv...> [--lag N] [--win W] [--tag name] [--maps] [--dump out.csv]
import sys, numpy as np, pandas as pd
from scipy.signal import savgol_filter

args = [a for a in sys.argv[1:] if not a.startswith("--")]
opt = {a.split("=")[0][2:]: (a.split("=")[1] if "=" in a else "1") for a in sys.argv[1:] if a.startswith("--")}
WIN = int(opt.get("win", 9)); POLY = 3
FIXLAG = int(opt["lag"]) if "lag" in opt else None

def segments(t, dtmax=0.03):
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

frames = []
for f in args:
    d = pd.read_csv(f); d["src"] = f.split("/")[-1]
    t = d["t"].values
    for ax in "pqr":
        d[ax + "dot_m"] = deriv(t, d[ax].values)
    d["nz_s"] = smooth(t, d["nz"].values)
    # lag: shift the prediction by k samples (positive = prediction leads the measurement)
    frames.append(d)
D = pd.concat(frames, ignore_index=True)
# drop spike samples (crash / respawn / teleport): measured accel beyond any aerodynamic value, or nz discontinuity
bad = (D["pdot_m"].abs() > 1500) | (D["qdot_m"].abs() > 800) | (D["rdot_m"].abs() > 400) | (D["nz"].diff().abs() > 1.5)
for ax in "pqr": D.loc[bad, ax + "dot_m"] = np.nan
D.loc[bad, "nz_s"] = np.nan
CLIP = opt.get("clip")
if CLIP: D = D[eval(CLIP)].copy()
print(f"# dropped {bad.sum()} spike samples")

def shifted(D, col, k):
    out = np.full(len(D), np.nan)
    for src, g in D.groupby("src"):
        idx = g.index.values; v = g[col].values
        for seg in segments(g["t"].values):
            s = v[seg]
            sh = np.full(len(s), np.nan)
            if k >= 0: sh[k:] = s[:len(s) - k] if k > 0 else s
            else: sh[:k] = s[-k:]
            out[idx[seg]] = sh
    return out

def rms(a, b):
    m = np.isfinite(a) & np.isfinite(b)
    return np.sqrt(np.mean((a[m] - b[m]) ** 2)), m.sum()

print(f"# {len(D)} samples from {D['src'].nunique()} logs; SG window {WIN} ({WIN/60:.2f} s), poly {POLY}")
# lag scan
best = {}
for ax, pred, meas in [("p", "pdot", "pdot_m"), ("q", "qdot", "qdot_m"), ("r", "rdot", "rdot_m")]:
    row = []
    for k in range(-4, 7):
        e, n = rms(shifted(D, pred, k), D[meas].values)
        row.append((k, e))
    kb = min(row, key=lambda x: x[1]) if FIXLAG is None else (FIXLAG, dict(row)[FIXLAG])
    best[ax] = kb[0]
    print(f"lag scan {ax}dot: " + " ".join(f"{k:+d}:{e:6.1f}" for k, e in row) + f"   -> lag {kb[0]} ({kb[0]/60*1000:.0f} ms)")
sd_m = {ax: np.nanstd(D[ax + "dot_m"]) for ax in "pqr"}
print("\n== RMS error (deg/s^2) at best lag, vs the measured accel's own spread ==")
tot = {}
for ax in "pqr":
    k = best[ax]
    pr = shifted(D, ax + "dot", k); me = D[ax + "dot_m"].values
    e, n = rms(pr, me)
    m = np.isfinite(pr) & np.isfinite(me)
    r2 = 1 - np.sum((pr[m] - me[m]) ** 2) / np.sum((me[m] - me[m].mean()) ** 2)
    # best single scale
    s = np.sum(pr[m] * me[m]) / np.sum(pr[m] ** 2)
    print(f"{ax}dot: RMS {e:7.1f}  std(meas) {sd_m[ax]:7.1f}  R2 {r2:6.3f}  LS scale meas/pred {s:5.3f}  n {n}")
    tot[ax] = (e, r2, s)
e, n = rms(D["nzp"].values, D["nz_s"].values)
m = np.isfinite(D["nzp"].values) & np.isfinite(D["nz_s"].values)
s = np.sum(D["nzp"].values[m] * D["nz_s"].values[m]) / np.sum(D["nzp"].values[m] ** 2)
print(f"nz:   RMS {e:7.3f} g  std(meas) {np.nanstd(D['nz_s']):6.3f}  LS scale meas/pred {s:5.3f}  mean pred {np.nanmean(D['nzp']):.3f} mean meas {np.nanmean(D['nz_s']):.3f}")
tot["nz"] = (e, s)

for ax in "pqr": D[ax + "dot_s"] = shifted(D, ax + "dot", best[ax])
if "maps" in opt:
    def table(by, bins, cols):
        D["_b"] = pd.cut(D[by], bins)
        g = D.groupby("_b", observed=True)
        rows = []
        for b, gg in g:
            if len(gg) < 30: continue
            r = {by: str(b), "n": len(gg)}
            for c, (pr, me) in cols.items():
                e, _ = rms(gg[pr].values, gg[me].values)
                mm = np.isfinite(gg[pr].values) & np.isfinite(gg[me].values)
                r[c + "_rms"] = round(e, 2); r[c + "_bias"] = round(np.mean((gg[pr].values - gg[me].values)[mm]), 2)
                r[c + "_sd"] = round(np.nanstd(gg[me].values), 2)
            rows.append(r)
        return pd.DataFrame(rows).to_string(index=False)
    cols = {"p": ("pdot_s", "pdot_m"), "q": ("qdot_s", "qdot_m"), "r": ("rdot_s", "rdot_m"), "nz": ("nzp", "nz_s")}
    print("\n== error map by AoA (bias = pred - meas) ==")
    print(table("alpha", [-45, -20, -10, 0, 5, 10, 15, 20, 25, 30, 36, 45, 55, 65, 90], cols))
    print("\n== by sideslip ==")
    print(table("beta", [-20, -10, -6, -3, -1, 1, 3, 6, 10, 20], cols))
    print("\n== by dynamic pressure (Pa) ==")
    print(table("qbar", [0, 3000, 6000, 10000, 15000, 25000, 40000, 80000], cols))
    print("\n== by Mach ==")
    print(table("mach", [0, 0.3, 0.45, 0.6, 0.75, 0.9, 1.1, 2], cols))
    # key slices
    print("\n== nz vs AoA at 8-15 kPa (pred vs meas, mean per bin) ==")
    s = D[(D.qbar > 8000) & (D.qbar < 15000) & (D.beta.abs() < 4)]
    s = s.assign(_b=pd.cut(s.alpha, [-10, 0, 5, 10, 15, 20, 25, 30, 36, 45, 55, 65]))
    print(s.groupby("_b", observed=True).agg(n=("nz", "size"), qbar=("qbar", "mean"), nz_meas=("nz_s", "mean"), nz_pred=("nzp", "mean"), hl=("hl", "mean")).round(3).to_string())
    print("\n== yaw accel vs sideslip, |p|,|q|,|r| < 15 deg/s and rudders within 3 deg (weathervane) ==")
    s = D[(D.p.abs() < 15) & (D.q.abs() < 15) & (D.r.abs() < 15) & (D.rudL.abs() < 3) & (D.rudR.abs() < 3) & (D.qbar > 3000)]
    s = s.assign(_b=pd.cut(s.beta, [-15, -8, -5, -3, -1.5, -0.5, 0.5, 1.5, 3, 5, 8, 15]))
    print(s.groupby("_b", observed=True).agg(n=("beta", "size"), alpha=("alpha", "mean"), qbar=("qbar", "mean"), rdot_meas=("rdot_m", "mean"), rdot_pred=("rdot_s", "mean")).round(2).to_string())
    print("\n== yaw accel vs yaw rate, |beta| < 2, rudders within 3 deg (damping) ==")
    s = D[(D.beta.abs() < 2) & (D.rudL.abs() < 3) & (D.rudR.abs() < 3) & (D.qbar > 3000) & (D.p.abs() < 20)]
    s = s.assign(_b=pd.cut(s.r, [-70, -40, -25, -15, -8, -3, 3, 8, 15, 25, 40, 70]))
    print(s.groupby("_b", observed=True).agg(n=("r", "size"), alpha=("alpha", "mean"), rdot_meas=("rdot_m", "mean"), rdot_pred=("rdot_s", "mean")).round(2).to_string())
    print("\n== roll accel vs roll rate, |beta|<3, lateral surfaces within 3 deg (roll damping) ==")
    s = D[(D.beta.abs() < 3) & ((D.ailL + D.ailR).abs() < 3) & ((D.flapL + D.flapR).abs() < 3) & ((D.stabL - D.stabR).abs() < 3) & (D.qbar > 3000)]
    s = s.assign(_b=pd.cut(s.p, [-200, -120, -80, -40, -15, 15, 40, 80, 120, 200]))
    print(s.groupby("_b", observed=True).agg(n=("p", "size"), alpha=("alpha", "mean"), pdot_meas=("pdot_m", "mean"), pdot_pred=("pdot_s", "mean")).round(2).to_string())

if "dump" in opt:
    D.to_csv(opt["dump"], index=False)
