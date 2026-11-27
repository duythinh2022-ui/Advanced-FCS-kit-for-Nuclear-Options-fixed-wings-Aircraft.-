import subprocess,sys,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
tag=sys.argv[1]; ex=sys.argv[2:]
cases=[(450,0.1,0.9),(400,0.2,0.9),(330,0.28,1.0),(260,0.33,0.9),(230,0.35,0.9),(230,0.35,0.3),(600,0.15,1.0)]
def run(c):
  ias,p,thr=c; f=f'runs/st_{tag}_{ias}_{p}_{thr}.csv'
  subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50',f'iasKmh={ias}',f'throttle={thr}','trimT=2','hold=6','post=0',f'prof=0:{p},6:{p}','roll=0','rollT=0.1',f'csv={f}']+ex,capture_output=True)
  d=pd.read_csv(f); d['t']=d.t+6; w=d[(d.t>4.5)&(d.t<6)]
  return dict(ias=ias,thr=thr,a=w.alpha.mean(),q=w.q.mean(),qsd=w.q.std(),tvc=w.tvc.mean(),stab=((w.stabL+w.stabR)/2).mean())
with ThreadPoolExecutor(2) as e: R=pd.DataFrame(list(e.map(run,cases)))
print(tag); print(R.round(2).to_string(index=False))
