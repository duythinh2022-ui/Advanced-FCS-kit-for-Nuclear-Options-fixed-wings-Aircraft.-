import subprocess,sys,pandas as pd,numpy as np
tag=sys.argv[1]; ex=sys.argv[2:]
out=[]
for ias,p,thr in [(250,0.3,0.2),(250,0.3,0.9),(200,0.55,0.2),(200,0.55,0.9),(300,0.25,0.2),(400,0.15,0.2)]:
  f=f'runs/ro_{tag}_{ias}_{p}_{thr}.csv'
  subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50',f'iasKmh={ias}',f'throttle={thr}','trimT=2','hold=5','post=0','rollFromPull=0','rollT=5',f'prof=0:{p},5:{p}','rprof=0:0,3:0,3.05:1,5:1',f'csv={f}']+ex,capture_output=True)
  d=pd.read_csv(f); d['t']=d.t+5; b=d[(d.t>2.6)&(d.t<3.0)]; w=d[(d.t>=3.0)&(d.t<4.0)]
  out.append(dict(ias=ias,thr=thr,a=b.alpha.mean(),tvc0=b.tvc.mean(),tvcDev=(w.tvc-b.tvc.mean()).abs().max(),stabMeanDev=((w.stabL+w.stabR)/2-((b.stabL+b.stabR)/2).mean()).abs().max(),qDev=(w.q-b.q.mean()).abs().max(),p1=w.p.abs().iloc[-1],bmax=w.beta.abs().max()))
R=pd.DataFrame(out); print(tag); print(R.round(1).to_string(index=False))
