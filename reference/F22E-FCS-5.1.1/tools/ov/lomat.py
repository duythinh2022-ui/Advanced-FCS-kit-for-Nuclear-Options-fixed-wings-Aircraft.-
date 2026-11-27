#!/usr/bin/env python3
# low-AoA roll-stop matrix: pitch stick held at p (0..0.3) from t=0; full roll from 1.5 s for T s; release; 3 s after.
# prints worst |beta| after release, AoA at release, roll rate at release. usage: lomat.py bin tag [Name=value ...]
import sys,os,subprocess,itertools,pandas as pd
from concurrent.futures import ThreadPoolExecutor
B=sys.argv[1]; tag=sys.argv[2]; ex=sys.argv[3:]; env=dict(os.environ)
ias=[float(x) for x in os.environ.get('IAS','450').split(',')]
def run(a):
    i,p,T=a; e=1.5+T; csv=f'/tmp/lm_{tag}_{i:.0f}_{p}_{T}.csv'
    args=['dotnet','bin/bench.dll','one',f'iasKmh={i}','trimT=3','throttle=-1','thr2=1','thr2T=0',f'prof=0:{p},9:{p}',f'rprof=0:0,1.5:0,1.51:1,{e}:1,{e+0.01}:0',f'hold={e}','post=3','DisableTVC=true','AoAPos=36','NoTvcAoACapPos=36','NoTvcThrCapPos=36',f'csv={csv}']+ex
    subprocess.run(args,cwd=B,env=env,capture_output=True)
    d=pd.read_csv(csv); r=d.iloc[d.t.abs().argmin()]; a2=d[d.t>=0]
    return (i,p,T,a2.beta.abs().max(),r.alpha,r.pS,d[d.t<0].beta.abs().max())
jobs=list(itertools.product(ias,[0.0,0.1,0.2,0.3],[1.5,2.0,2.5,3.0]))
with ThreadPoolExecutor(2) as ex_: res=list(ex_.map(run,jobs))
import numpy as np
b=np.array([r[3] for r in res])
print(f'{tag:14s} stop sideslip worst {b.max():5.1f} mean {b.mean():4.1f} | in-roll worst {max(r[6] for r in res):4.1f}')
if os.environ.get('V'):
    for r in res: print('  ias %.0f p %.1f T %.1f  beta %5.1f  aoa %5.1f  pS %5.0f  inroll %4.1f'%r)
