#!/usr/bin/env python3
# idrun.py <tag> <bindir> [Name=value ...]: pinned + free replays [t-0.5, t+3] of every push in the 2.35.x flights and hitch_all.csv
import sys, os, subprocess, csv
from concurrent.futures import ThreadPoolExecutor
R='/home/claude/w/src/F22E_FCS_2.31.1_src/bench'; tag=sys.argv[1]; bind=sys.argv[2]; kv=sys.argv[3:]
O=f'/home/claude/w/score/id_{tag}'; os.makedirs(O,exist_ok=True)
sets={'223711':[31.45,39.17,54.35,61.77,69.65,74.02,87.15,97.12,112.96],'225204':[43.15,84.42,93.31],
 '231000':[31.68,43.77,47.3,59.78,70.4,86.74,97.21,118.28,124.3,142.06,177.05,183.47,223.72],'232903':[55.4,59.38,65.02,79.92,88.37,136.13],'233737':[47.17,50.62]}
for r in csv.DictReader(open('/home/claude/w/hitch_all.csv')): sets.setdefault(r['log'],[]).append(float(r['t']))
jobs=[]
for lg,ts in sets.items():
  for t in ts:
    for m in ('pin','free'):
      jobs.append(['dotnet',f'{bind}/bench.dll','replay',f'log=/home/claude/w/logs/F22E_FCS_20261103_{lg}.csv',f't0={t-0.5:.3f}',f't1={t+3.0:.3f}',f'out={O}/{lg}_{t}_{m}.csv']+(['pinPitch=1'] if m=='pin' else [])+kv)
env=dict(os.environ,BENCH_DATA=R+'/data/airframe_cal.json')
with ThreadPoolExecutor(3) as ex: rc=list(ex.map(lambda c: subprocess.run(c,cwd=R,env=env,capture_output=True).returncode,jobs))
print(tag,len(jobs),'runs, failures',sum(1 for x in rc if x))
