# bench experiments, not shipped: low-throttle overshoot cases on 3.2.0 + variants
import subprocess,os,sys
import pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
W='/home/claude/w'; S=W+'/acc/out_exp'; os.makedirs(S,exist_ok=True)
V={'cur':(W+'/base/bench',''),'P':(W+'/f22/bench',''),'X2b':(W+'/f22/bench',open(W+'/acc/X2b.args').read().strip())}
C={'hold30_0':'AoAPos=30 iasKmh=300 gamma0=0 throttle=0 trimT=3 hold=6 post=0 prof=0:1,6:1 roll=0 rollFromPull=0 rprof=0:0',
'hold45_0':'AoAPos=45 iasKmh=300 gamma0=0 throttle=0 trimT=3 hold=6 post=0 prof=0:1,6:1 roll=0 rollFromPull=0 rprof=0:0',
'corkthr15_400':'iasKmh=400 alt=1500 trimT=1 pull=0 hold=0 roll=0 post=6 throttle=0.15 prof=0:0,0.05:1,6:1 rprof=0:0,0.3:0,0.35:1,6:1',
'pullroll_400':'iasKmh=400 alt=1500 trimT=1 pull=0 hold=0 roll=0 post=7.5 prof=0:0,0.05:1,6:1 rprof=0:0,0.3:0,0.35:1,2.5:1,2.55:0,6:0',
'pullonly_400_idle':'iasKmh=400 alt=1500 trimT=1 pull=0 hold=0 roll=0 post=6 throttle=0 prof=0:0,0.05:1,6:1 rprof=0:0'}
def one(v,c):
    B,ex=V[v]; f=f'{S}/{v}_{c}.csv'
    subprocess.run(['dotnet','bin/bench.dll','one']+ex.split()+C[c].split()+[f'csv={f}'],cwd=B,capture_output=True,env=dict(os.environ,BENCH_DATA=B+'/data/airframe_cal.json'))
    d=pd.read_csv(f); w=d[d.t>=-7]; e=d.iloc[-1]; return dict(v=v,case=c,aMax=w.alpha.max(),aEnd=e.alpha,qMax=w.q.max(),last=d[d.t>d.t.max()-3].alpha.mean())
J=[(v,c) for c in C for v in V]
with ThreadPoolExecutor(2) as ex: R=list(ex.map(lambda j:one(*j),J))
r=pd.DataFrame(R); pd.set_option('display.width',200); print(r.pivot(index='case',columns='v',values='aMax').round(1).to_string()); print(r.pivot(index='case',columns='v',values='last').round(1).to_string()); 
