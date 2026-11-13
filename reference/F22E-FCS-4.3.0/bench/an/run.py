#!/usr/bin/env python3
# run.py <tag> [Name=value ...]: pinned + free replays of every push in hitch_all.csv, window [t-0.5, t+3.0]
import sys, os, subprocess, csv
from concurrent.futures import ThreadPoolExecutor
R = '/home/claude/w/src/F22E_FCS_2.31.1_src/bench'; tag = sys.argv[1]; extra = sys.argv[2:]
O = f'/home/claude/w/score/{tag}'; os.makedirs(O, exist_ok=True)
rows = list(csv.DictReader(open('/home/claude/w/hitch_all.csv')))
jobs = []
for r in rows:
    for m in ('pin', 'free'):
        t = float(r['t']); out = f"{O}/{r['log']}_{r['t']}_{m}.csv"
        cmd = ['dotnet', 'bin/bench.dll', 'replay', f"log=/home/claude/w/logs/F22E_FCS_20261103_{r['log']}.csv", f't0={t-0.5:.3f}', f't1={t+3.0:.3f}', f'out={out}'] + (['pinPitch=1'] if m == 'pin' else []) + extra
        jobs.append(cmd)
env = dict(os.environ, BENCH_DATA=R + '/data/airframe_cal.json')
def go(c): return subprocess.run(c, cwd=R, capture_output=True, text=True, env=env).returncode
with ThreadPoolExecutor(2) as ex: rc = list(ex.map(go, jobs))
print(tag, len(jobs), 'runs, failures', sum(1 for x in rc if x))
