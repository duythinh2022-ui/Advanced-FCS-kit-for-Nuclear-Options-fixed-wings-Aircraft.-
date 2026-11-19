#!/usr/bin/env python3
# replaycheck: the sixth suite block (Bench_leaf_calibration_handoff.md section 5). Every listed push is replayed closed-loop
# from the logged state (bench replay, warm) and compared with the game over the first 1.0 s and over the whole window.
#   replaycheck.py <bench dir> <logdir> <cases.json> <tag> [--jobs=2] [Name=value ...]
# cases.json: [{"log": "F22E_FCS_20261103_152507.csv", "build": "2.29.0", "pushes": [{"t": 33.95, "kind": "released"}, ...]}, ...]
# FAIL if, over the first 1.0 s after the stick: |q_b-q_g| > 5 deg/s for more than 3 consecutive ticks, or |r_b-r_g| > 5,
# or |p_b-p_g| > 10, or |beta_b-beta_g| > 2; and, over the whole window: the bench's yaw shed (mean r-dot against the rotation
# while 65 > alpha > 40) outside +-10 deg/s^2 of the game's, the body-roll peak through 50..35 deg outside +-10 deg/s of the
# game's, or the bench's capZeN minimum outside +-10 of the game's ceCapZeN minimum where the game's collapsed (< 30).
# Only logs whose build matches the source (bench/fcs version) count toward PASS; others are reported as "info".
import sys, os, json, subprocess, re, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
B, LOGDIR, CASES, TAG = sys.argv[1:5]
opt = {a.split("=")[0][2:]: a.split("=", 1)[1] for a in sys.argv[5:] if a.startswith("--")}
sets = [a for a in sys.argv[5:] if not a.startswith("--")]
JOBS = int(opt.get("jobs", "2"))
OUT = f"{B}/out/replaycheck/{TAG}"; os.makedirs(OUT, exist_ok=True)
env = dict(os.environ, BENCH_DATA=os.environ.get("BENCH_DATA", f"{B}/data/airframe_cal.json"))
src_ver = None
for cand in [f"{B}/fcs/FcsStandalonePlugin.cs", f"{B}/../Aryx_F22E_StrikeRaptor.FCS/FcsStandalonePlugin.cs"] + [os.path.join(r, f) for r in [f"{B}/fcs", f"{B}/.."] if os.path.isdir(r) for f in os.listdir(r) if f.endswith("Plugin.cs")]:
    try:
        m = re.search(r'Version\s*=\s*"(\d+\.\d+\.\d+)"', open(cand).read())
        if m: src_ver = m.group(1); break
    except Exception: pass
cases = json.load(open(CASES))

def replay(log, t, kind):
    out = f"{OUT}/{os.path.basename(log)[9:-4]}_{t:.2f}.csv"
    t0, t1 = t - 0.3, t + 2.5
    r = subprocess.run(["dotnet", f"{B}/bin/bench.dll", "replay", f"log={LOGDIR}/{log}", f"t0={t0}", f"t1={t1}", f"out={out}"] + sets, env=env, capture_output=True, text=True)
    if r.returncode != 0: return None, r.stderr[-300:]
    return out, None

def shed(d, lo, hi, col):
    w = d[(d.alpha_g < hi) & (d.alpha_g > lo)]
    if len(w) < 4: return np.nan
    r = w[col].values; t = w.t.values
    rd = np.gradient(r, t)
    sgn = -np.sign(np.mean(r)) if abs(np.mean(r)) > 1 else 1.0   # against the rotation
    return float(np.mean(rd) * sgn)

def score(f, log, t, kind):
    d = pd.read_csv(f); d = d[d.t >= t - 1e-6]
    g = pd.read_csv(f"{LOGDIR}/{log}", usecols=lambda c: c in ("t", "ceCapZeN")) if "ceCapZeN" in open(f"{LOGDIR}/{log}").readline() else None
    w1 = d[d.t <= t + 1.0]
    res = {}
    dq = (w1.q_b - w1.q_g).abs().values
    run = 0; worst = 0
    for v in dq:
        run = run + 1 if v > 5 else 0; worst = max(worst, run)
    res["q_run>5"] = worst
    res["r_max"] = float((w1.r_b - w1.r_g).abs().max()); res["p_max"] = float((w1.p_b - w1.p_g).abs().max()); res["beta_max"] = float((w1.beta_b - w1.beta_g).abs().max())
    res["shed_b"] = shed(d, 40, 65, "r_b"); res["shed_g"] = shed(d, 40, 65, "r_g")
    w = d[(d.alpha_g < 50) & (d.alpha_g > 35)]
    res["prollpk_b"] = float(w.p_b.abs().max()) if len(w) else np.nan; res["prollpk_g"] = float(w.p_g.abs().max()) if len(w) else np.nan
    if g is not None:
        gg = g[(g.t >= t) & (g.t <= t + 2.5)]
        res["capN_g_min"] = float(gg.ceCapZeN.min()) if len(gg) else np.nan
    else: res["capN_g_min"] = np.nan
    res["capN_b_min"] = float(d.capZeN_b.min())
    fails = []
    if res["q_run>5"] > 3: fails.append("q")
    if res["r_max"] > 5: fails.append("r")
    if res["p_max"] > 10: fails.append("p")
    if res["beta_max"] > 2: fails.append("beta")
    if np.isfinite(res["shed_b"]) and np.isfinite(res["shed_g"]) and abs(res["shed_b"] - res["shed_g"]) > 10: fails.append("shed")
    if np.isfinite(res["prollpk_b"]) and np.isfinite(res["prollpk_g"]) and abs(res["prollpk_b"] - res["prollpk_g"]) > 10: fails.append("prollpk")
    if np.isfinite(res["capN_g_min"]) and res["capN_g_min"] < 30 and abs(res["capN_b_min"] - res["capN_g_min"]) > 10: fails.append("capN")
    return res, fails

jobs = [(c["log"], c.get("build"), p["t"], p.get("kind", "released")) for c in cases for p in c["pushes"]]
with ThreadPoolExecutor(JOBS) as ex: outs = list(ex.map(lambda j: replay(j[0], j[2], j[3]), jobs))
lines = [f"# replaycheck {TAG}: source {src_ver or '?'}; {len(jobs)} pushes; sets {' '.join(sets) or '-'}",
         f"# first 1.0 s: q run>5 deg/s (ticks, FAIL>3) | r max | p max | beta max ; window: yaw shed 65..40 b/g | body roll peak 50..35 b/g | capZeN min b/g"]
npass = nfail = ninfo = 0
for (log, build, t, kind), (f, err) in zip(jobs, outs):
    counts = build is None or src_ver is None or build == src_ver
    if f is None: lines.append(f"{log[9:-4]} {t:7.2f} {kind:8s} ERROR {err}"); nfail += counts; continue
    res, fails = score(f, log, t, kind)
    st = ("PASS" if not fails else "FAIL") if counts else "info"
    if counts: npass += not fails; nfail += bool(fails)
    else: ninfo += 1
    lines.append(f"{log[9:-4]} {t:7.2f} {kind:8s} {build or '?':7s} {st:4s} {','.join(fails) or '-':16s} q {res['q_run>5']:2d} r {res['r_max']:5.1f} p {res['p_max']:5.1f} b {res['beta_max']:4.1f} | shed {res['shed_b']:6.1f}/{res['shed_g']:6.1f} | proll {res['prollpk_b']:5.1f}/{res['prollpk_g']:5.1f} | capN {res['capN_b_min']:5.1f}/{res['capN_g_min']:5.1f}")
lines.append(f"# {npass} PASS, {nfail} FAIL (same-build logs), {ninfo} info (other builds)")
open(f"{B}/out/out_replaycheck_{TAG}.txt", "w").write("\n".join(lines) + "\n")
print("\n".join(lines))
