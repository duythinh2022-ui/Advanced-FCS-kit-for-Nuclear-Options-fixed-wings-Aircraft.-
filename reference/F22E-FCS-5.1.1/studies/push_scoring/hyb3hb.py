import sys,os,subprocess,numpy as np,pandas as pd
from concurrent.futures import ThreadPoolExecutor
sys.path.insert(0,'.'); from s48 import sets
R=os.environ.get('BR')
tag=sys.argv[1]; b=sys.argv[2]; kv=sys.argv[3:]
S=dict(sets); S['20261104_090307']=[110.53,137.44,162.55]; S['20261104_093717']=[49.23]
jobs=[];meta=[]
os.makedirs(f'score/hyH_{tag}',exist_ok=True)
for lg,ts in S.items():
    L=lg if lg.startswith('2026') else '20261103_'+lg
    d=pd.read_csv(f'logs/F22E_FCS_{L}.csv',usecols=['t','sweepWin','sr','alpha'])
    for t in ts:
        w=d[(d.t>=t)&(d.t<=t+2)]
        if not (w.sr.abs()>0.1).mean()>0.5: continue
        on=w[w.sweepWin>0.5]
        if not len(on): continue
        tw=float(on.t.iloc[0]); meta.append((L,t,tw))
        # pin until the plan starts (t), free from there: the stab window under a plan acts from the plan's first tick
        jobs.append(['dotnet',f'{R}/{b}/bench.dll','replay',f'log=/home/claude/w/logs/F22E_FCS_{L}.csv',f't0={t-0.5:.3f}',f't1={t+3:.3f}',f'out=/home/claude/w/score/hyH_{tag}/{L}_{t}.csv',f'pinUntil={t-0.02:.3f}']+kv)
env=dict(os.environ,BENCH_DATA=R+'/data/airframe_cal.json')
with ThreadPoolExecutor(3) as ex: rc=list(ex.map(lambda c: subprocess.run(c,cwd=R,env=env,capture_output=True).returncode,jobs))
print(tag,'runs',len(jobs),'fail',sum(1 for x in rc if x))
