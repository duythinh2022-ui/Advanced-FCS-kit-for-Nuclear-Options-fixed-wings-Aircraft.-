#!/usr/bin/env python3
# Closed-loop replay of the handoff's test cases: replay_eval.py <bench dir> <bench.dll dir> <airframe json> <cases.json> <tag> [Name=value ...]
import sys, os, json, subprocess, numpy as np, pandas as pd
B, BIN, DATA, CASES, TAG = sys.argv[1:6]; extra = sys.argv[6:]
cases = json.load(open(CASES))
LOGDIR = os.environ.get("FCS_LOGDIR", "/mnt/user-data/uploads/Nuclear Option/BepInEx")
OUT = f"{os.environ.get('REP_OUT', 'rep')}/{TAG}"; os.makedirs(OUT, exist_ok=True)
env = dict(os.environ, BENCH_DATA=DATA)
rows = []
for c in cases:
    out = f"{OUT}/{c['name']}.csv"
    p = subprocess.run(["dotnet", f"{BIN}/bench.dll", "replay", f"log={LOGDIR}/{c['log']}", f"t0={c['t0']}", f"t1={c['t1']}", f"out={out}"] + extra, env=env, capture_output=True, text=True)
    if not os.path.exists(out): print(c["name"], "FAILED", p.stderr[-500:]); continue
    d = pd.read_csv(out)
    d = d[d.t >= c["t0"] + 0.1]
    def rms(a, b): return float(np.sqrt(np.mean((d[a] - d[b]) ** 2)))
    r = dict(case=c["name"], desc=c.get("desc", ""), T=round(c["t1"] - c["t0"], 1),
             beta_rms=rms("beta_b", "beta_g"), beta_pk_g=float(d.beta_g.abs().max()), beta_pk_b=float(d.beta_b.abs().max()),
             p_rms=rms("p_b", "p_g"), p_pk_g=float(d.p_g.abs().max()), p_pk_b=float(d.p_b.abs().max()),
             alpha_rms=rms("alpha_b", "alpha_g"), alpha_end_g=float(d.alpha_g.iloc[-1]), alpha_end_b=float(d.alpha_b.iloc[-1]),
             nz_rms=rms("nz_b", "nz_g"), q_rms=rms("q_b", "q_g"), r_rms=rms("r_b", "r_g"), ias_end_err=float(d.ias_b.iloc[-1] - d.ias_g.iloc[-1]))
    rows.append(r)
R = pd.DataFrame(rows)
pd.set_option("display.width", 250)
print(R.round(2).to_string(index=False))
R.to_csv(f"{OUT}/summary.csv", index=False)
print("\nmean: beta RMS %.2f (peak err %.2f), p RMS %.1f, alpha RMS %.2f, nz RMS %.2f" % (R.beta_rms.mean(), (R.beta_pk_b - R.beta_pk_g).abs().mean(), R.p_rms.mean(), R.alpha_rms.mean(), R.nz_rms.mean()))
