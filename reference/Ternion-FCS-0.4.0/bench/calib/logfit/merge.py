# merge.py <tag> : interpolate the 4 fuel-level eqerr runs on the logged mass, attach measured accelerations -> cal/<tag>.pkl
import sys, glob, os, numpy as np, pandas as pd
sys.path.insert(0, "/home/claude/tern/w/bench/calib"); import eqcore
tag = sys.argv[1]; O = f"/home/claude/tern/cal/{tag}"; L = "/home/claude/tern/logs/"
F = [0.05, 0.20, 0.35, 0.50]; M = [19690 + 12000 * f for f in F]
logs = sorted({os.path.basename(f).split("_f")[0] for f in glob.glob(O + "/*_f*.csv")})
keep = ["pdot", "qdot", "rdot", "nzp", "nyp", "Mx", "My", "Mz"]
out = []
for lg in logs:
    runs = [pd.read_csv(f"{O}/{lg}_f{f:.2f}.csv") for f in F]
    base = runs[0].copy()
    lc = pd.read_csv(L + f"Ternion_FCS_{lg}.csv", low_memory=False)
    lc = lc[["t", "massKg", "asymDy", "engOut", "dmgGate", "yawHealth", "gearDown"] if "gearDown" in lc.columns else ["t", "massKg", "asymDy", "engOut", "dmgGate", "yawHealth"]]
    base = base.merge(lc.drop_duplicates("t"), on="t", how="left")
    m = base.massKg.values
    # flights split at mass jumps up (respawn); exclude everything after a sudden drop > 150 kg in < 0.5 s (parts lost / stores off)
    t = base.t.values
    bad = np.zeros(len(base), bool); lost = False
    for i in range(1, len(base)):
        if t[i] - t[i - 1] > 5 or m[i] - m[i - 1] > 300: lost = False
        if m[i - 1] - m[i] > 150: lost = True
        bad[i] = lost
    dmg = np.zeros(len(base), bool); seen = False
    for i in range(len(base)):
        if i and (t[i] - t[i - 1] > 5 or m[i] - m[i - 1] > 300): seen = False
        if base.dmgGate.values[i] > 0 or base.engOut.values[i] > 0 or base.yawHealth.values[i] < 1: seen = True
        dmg[i] = seen
    base["excl"] = bad | dmg | (m < 19650) | (m > 25750) | (base.asymDy.abs() > 0.03)
    w = np.clip(np.interp(m, M, np.arange(4)), 0, 3)
    i0 = np.minimum(w.astype(int), 2); fr = w - i0
    for k in keep:
        V = np.column_stack([r[k].values for r in runs])
        base[k] = V[np.arange(len(base)), i0] * (1 - fr) + V[np.arange(len(base)), i0 + 1] * fr
    base["src"] = lg
    out.append(base)
D = pd.concat(out, ignore_index=True)
# measured accelerations per log (segments split on time gaps)
parts = []
for lg, d in D.groupby("src", sort=False):
    d = d.copy(); tt = d.t.values
    for ax in "pqr": d[ax + "dot_m"] = eqcore.deriv(tt, d[ax].values)
    d["nz_s"] = eqcore.smooth(tt, d.nz.values); parts.append(d)
D = pd.concat(parts, ignore_index=True)
spike = (D.pdot_m.abs() > 1500) | (D.qdot_m.abs() > 800) | (D.rdot_m.abs() > 400)
for ax in "pqr": D.loc[spike, ax + "dot_m"] = np.nan
D.to_pickle(f"/home/claude/tern/cal/{tag}.pkl")
U = D[~D.excl]
print(f"{tag}: {len(D)} rows, {len(U)} used ({len(U)/60:.0f} s); mass {U.massKg.min():.0f}-{U.massKg.max():.0f}")
