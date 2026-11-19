import subprocess,sys,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
tag=sys.argv[1]; ex=sys.argv[2:]
cases=[(230,0.35,0.9),(260,0.33,0.9),(300,0.3,0.9),(230,0.35,0.3),(400,0.2,0.9),(330,0.28,1.0)]
def run(c):
  ias,p,thr=c; f=f'runs/r36_{tag}_{ias}_{p}_{thr}.csv'
  subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50',f'iasKmh={ias}',f'throttle={thr}','trimT=2','hold=5','post=0','rollFromPull=0','rollT=5',f'prof=0:{p},5:{p}','rprof=0:0,3:0,3.05:1,5:1',f'csv={f}']+ex,capture_output=True)
  d=pd.read_csv(f); d['t']=d.t+5; b=d[(d.t>2.6)&(d.t<3.0)]; w=d[(d.t>=3.0)&(d.t<4.5)]
  sm=lambda x:(x.stabL+x.stabR)/2
  a=np.radians(w.alpha); ps=(w.p*np.cos(a)+w.r*np.sin(a)).abs()
  return dict(ias=ias,thr=thr,a=b.alpha.mean(),q=b.q.mean(),tvcB=b.tvc.mean(),tvcDev=(w.tvc-b.tvc.mean()).abs().max(),stabMDev=(sm(w)-sm(b).mean()).abs().max(),split=((w.stabL-w.stabR)/2).abs().max(),qDev=(w.q-b.q.mean()).abs().max(),pS1=ps.iloc[int(1/0.0167)],pSmax=ps.max(),bmax=w.beta.abs().max())
with ThreadPoolExecutor(2) as e: R=pd.DataFrame(list(e.map(run,cases)))
print(tag,'| mean tvcDev %.1f stabMDev %.1f split %.1f qDev %.1f pS@1s %.0f bmax %.1f'%(R.tvcDev.mean(),R.stabMDev.mean(),R.split.mean(),R.qDev.mean(),R.pS1.mean(),R.bmax.mean()))
print(R.round(1).to_string(index=False))
