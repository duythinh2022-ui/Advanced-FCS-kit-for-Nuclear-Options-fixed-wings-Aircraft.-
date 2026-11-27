# corkscrew entries: full lateral with a pull from 1 g; roll profile, AoA/G overshoot, sideslip. corkentry.py <benchdir> <tag> [Name=value ...]
import sys, os, subprocess, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
B=sys.argv[1]; tag=sys.argv[2]; sets=sys.argv[3:]; O=os.environ.get('REG_S','/tmp')+f'/cork/{tag}'; os.makedirs(O,exist_ok=True)
env=dict(os.environ)  # BENCH_DATA from the environment (suite.sh sets it)
C=[]
for ias in (400,556,630,700,850):
    for ps,thr in ((1.0,1.0),(1.0,0.3),(1.0,0.15),(0.7,1.0)):
        C.append((f'pull{ps} thr{thr} {ias}',ias,ps,thr))
def run(c):
    n,ias,ps,thr=c; f=f'{O}/{n.replace(" ","_")}.csv'
    if not os.path.exists(f):
        subprocess.run(['dotnet',B+'/bin/bench.dll','one',f'iasKmh={ias}','alt=1500','trimT=1','pull=0','hold=0','roll=0','post=6',f'throttle={thr}',f'prof=0:0,0.05:{ps},6:{ps}','rprof=0:0,0.3:0,0.35:1,6:1',f'csv={f}']+sets,cwd=B,env=env,capture_output=True)
    d=pd.read_csv(f); w=d[(d.t>0.35)&(d.t<4.5)]; tw=round(w.tw.median(),2) if 'tw' in w else 0
    ps_=w.pS.abs()
    e=w[w.t<1.5]; pk=e.pS.abs().max(); tpk=e.loc[e.pS.abs().idxmax(),'t']; after=w[w.t>tpk]
    dip=after[after.t<3.5].pS.abs().min() if len(after) else np.nan
    return dict(case=n,tw=round(d[(d.t>1)&(d.t<3)].tw.median(),2) if 'tw' in d else 0,pkEarly=round(pk),dip=round(dip),mean=round(ps_.mean()),pAt2s=round(abs(d[d.t>=2.0].pS.iloc[0])),aMax=round(w.alpha.max(),1),aOver=round((w.alpha-w.alphaCmd).max(),1),nzMax=round(w.nz.max(),2),beta=round(w.beta.abs().max(),1),capMin=round(w.coupLim[w.coupLim>0].min()) if (w.coupLim>0).any() else 0)
with ThreadPoolExecutor(2) as ex: rows=list(ex.map(run,C))
r=pd.DataFrame(rows); r.to_csv(f'{O}/summary.csv',index=False); print(tag,' '.join(sets)); print(r.to_string(index=False))
