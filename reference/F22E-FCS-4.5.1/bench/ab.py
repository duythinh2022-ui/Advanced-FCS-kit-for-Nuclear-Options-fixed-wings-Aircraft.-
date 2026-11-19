import subprocess,sys,pandas as pd,os,numpy as np,warnings; warnings.filterwarnings('ignore')
from concurrent.futures import ThreadPoolExecutor
# airbrake held from t=0, pitch stick sp held 6 s (AoA hold at sp), full lateral from 2 s to 4 s; scores
tag=sys.argv[1]; ex=sys.argv[2:]
cases=[(ias,sp) for ias in [350,500,700] for sp in [0.0,0.3,0.6,1.0]]
def run(c):
  ias,sp=c; f=f'runs/ab_{tag}_{ias}_{sp}.csv'
  if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50',f'iasKmh={ias}','trimT=1','hold=6','post=0','rollFromPull=0','rollT=6','throttle=0.3',f'prof=0:{sp},6:{sp}','rprof=0:0,2:0,2.05:1,4:1,4.05:0,6:0','bprof=0:1,6:1',f'csv={f}']+ex,capture_output=True)
  d=pd.read_csv(f); d['t']=d.t+6; w=d[d.t>=0]; r=w[(w.t>=2)&(w.t<=4)]; s=w[(w.t>=1)&(w.t<2)]
  return dict(ias=ias,sp=sp,a=s.alpha.mean(),share=s.abShare.mean(),open=s.abOpen.mean(),ax=s.ax.mean(),ail=((s.ailL+s.ailR)/2).mean(),p1=r[r.t<=3].p.abs().max(),pmax=r.p.abs().max(),bmax=w.beta.abs().max(),dIas=w.ias.iloc[-1]-w.ias.iloc[0])
with ThreadPoolExecutor(4) as x: R=pd.DataFrame(list(x.map(run,cases)))
print(tag); print(R.round(2).to_string(index=False))
