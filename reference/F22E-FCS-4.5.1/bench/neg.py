import subprocess,sys,os,itertools,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
tag=sys.argv[1]; ex=sys.argv[2:]
cases=list(itertools.product([200,300,450,600],[-1,-0.5,-0.2],[0,0.5]))
def run(c):
  ias,p,rd=c; f=f'runs/neg_{tag}_{ias}_{p}_{rd}.csv'
  if not os.path.exists(f):
    subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50','throttle=1','trimT=2','hold=5','post=0','rollFromPull=0','rollT=5',f'iasKmh={ias}',f'prof=0:{p},5:{p}',f'rprof=0:0,{rd}:0,{rd+0.05}:1,5:1',f'csv={f}']+ex,capture_output=True)
  d=pd.read_csv(f); d['t']=d.t+5; w=d[d.t>=rd]
  return dict(ias=ias,p=p,rd=rd,bmax=w.beta.abs().max(),amin=w.alpha.min(),bank2=w.phi.iloc[min(len(w)-1,int(2/0.0167))],pmean=w.p.abs().mean())
with ThreadPoolExecutor(2) as e: R=pd.DataFrame(list(e.map(run,cases)))
R.to_csv(f'neg_{tag}.csv',index=False)
print(tag,'bmax mean %.1f worst %.1f |p| mean %.0f'%(R.bmax.mean(),R.bmax.max(),R.pmean.mean()))
