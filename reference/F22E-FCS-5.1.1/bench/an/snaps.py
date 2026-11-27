import json, os, subprocess, sys, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0, '/home/claude/w'); from snaprel import metrics
R = '/home/claude/w/src/F22E_FCS_2.31.1_src/bench'; env = dict(os.environ, BENCH_DATA=R + '/data/airframe_cal.json')
cases = json.load(open('/home/claude/w/push/snaps.json')); O = '/home/claude/w/score/snaps'; os.makedirs(O, exist_ok=True)
jobs = []
for log, t in cases:
    for tag, kv in (('base', 'PushGov=false'), ('gov', 'PushGov=true')):
        jobs.append((['dotnet', 'bin/bench.dll', 'replay', f'log=/home/claude/w/logs/F22E_FCS_20261103_{log}.csv', f't0={t-0.5:.3f}', f't1={t+2.0:.3f}', f'out={O}/{log}_{t}_{tag}.csv', kv], (log, t, tag)))
with ThreadPoolExecutor(2) as ex: list(ex.map(lambda j: subprocess.run(j[0], cwd=R, env=env, capture_output=True), jobs))
rows = []
for log, t in cases:
    x = dict(case=f'{log} {t}')
    for tag in ('base', 'gov'):
        d = pd.read_csv(f'{O}/{log}_{t}_{tag}.csv'); back, bsw = metrics(d.t, d.r_b, d.beta_b, t)
        x[tag + '_yawback'] = round(back, 1); x[tag + '_betaswing'] = round(bsw, 1); x[tag + '_betapk'] = round(d[d.t >= t].beta_b.abs().max(), 1)
    rows.append(x)
df = pd.DataFrame(rows); print(df.to_string(index=False)); print(df.drop(columns='case').median().round(2).to_string())
