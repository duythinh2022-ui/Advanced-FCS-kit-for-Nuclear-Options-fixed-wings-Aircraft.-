import subprocess,sys,pandas as pd,os,numpy as np,warnings; warnings.filterwarnings('ignore')
from concurrent.futures import ThreadPoolExecutor
# full aft + full roll held H s, then everything released; scores the 4 s after the release
tag=sys.argv[1]; ex=sys.argv[2:]
cases=[(ias,H,rs,thr) for ias in [244,250,256,344,350,356,444,450,456,594,600,606] for H in [1.5,3.0] for rs in [1,-1] for thr in [0.3,1.0]]
def run(c):
  ias,H,rs,thr=c; f=f'runs/relj_{tag}_{ias}_{H}_{rs}_{thr}.csv'
  T=H+4
  if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50',f'iasKmh={ias}','trimT=1',f'hold={T}','post=0','rollFromPull=0',f'rollT={T}',f'throttle={thr}',f'prof=0:1,{H}:1,{H+0.05}:0,{T}:0',f'rprof=0:{rs},{H}:{rs},{H+0.05}:0,{T}:0',f'csv={f}']+ex,capture_output=True)
  d=pd.read_csv(f); d['t']=d.t+T; w=d[d.t>=H]; e=d[d.t>=H+2.5]
  return dict(ias=ias,H=H,rs=rs,thr=thr,aRel=d[d.t<=H].alpha.iloc[-1],pOver=(-np.sign(rs)*w.p).max(),qMin=w.q.min(),bmax=w.beta.abs().max(),
              split=((e.stabL-e.stabR)/2).abs().mean(),flap=e.flapL.abs().mean(),ail=e.ailL.abs().mean(),rud=(e.rudL-e.rudR).abs().mean(),pEnd=e.p.abs().mean())
with ThreadPoolExecutor(4) as x: R=pd.DataFrame(list(x.map(run,cases)))
R.to_csv(f'relj_{tag}.csv',index=False)
print(tag, R[['pOver','qMin','bmax','split','flap','ail','rud','pEnd']].agg(['mean','max']).round(2).to_string())
