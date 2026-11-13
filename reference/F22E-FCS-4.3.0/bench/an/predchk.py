# predicted vs realized reversal sideslip: predchk.py <benchdir> <tag> [Name=value ...]
import sys, os, subprocess, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
B=sys.argv[1]; tag=sys.argv[2]; sets=sys.argv[3:]; O=f'/tmp/claude-0/pred/{tag}'; os.makedirs(O,exist_ok=True)
env=dict(os.environ,BENCH_DATA='/tmp/claude-0/-home-claude/c3114abd-20fc-5984-be11-c25d56f03f2d/scratchpad/airframe_cal.json')
C=[]
for ias in (500,800): C.append((f'1g {ias}',ias,'0:0,8:0','0:0,0.05:1,1.5:1,1.55:-1,4:-1',1.55))
for ias in (400,556,700): C.append((f'0.3pull {ias}',ias,'0:0,0.05:0.3,7:0.3','0:0,1:0,1.05:1,3:1,3.05:-1,6:-1',3.05))
for ias in (400,556,700): C.append((f'0.5pull {ias}',ias,'0:0,0.05:0.5,7:0.5','0:0,1:0,1.05:1,3:1,3.05:-1,6:-1',3.05))
for ias in (300,400,500): C.append((f'0.75pull {ias}',ias,'0:0,0.05:0.75,7:0.75','0:0,1:0,1.05:1,3:1,3.05:-1,6:-1',3.05))
for ias in (300,400,500): C.append((f'fullaft {ias}',ias,'0:0,0.05:1,8:1','0:0,0.05:1,3:1,3.05:-1,6:-1',3.05))
for ias in (160,300): C.append((f'leaf {ias}',ias,'0:0,0.8:0,0.85:1,17:1','0:0,4:0,4.05:1,9:1,9.05:-1,15:-1',9.05))
def run(c):
    n,ias,prof,rprof,tr=c; f=f'{O}/{n.replace(" ","_")}.csv'
    if not os.path.exists(f):
        subprocess.run(['dotnet',B+'/bin/bench.dll','one',f'iasKmh={ias}','alt=1500','trimT=1','pull=0','hold=0','roll=0',f'post={tr+4}','throttle=1',f'prof={prof}',f'rprof={rprof}',f'csv={f}']+sets,cwd=B,env=env,capture_output=True)
    d=pd.read_csv(f); pre=d[(d.t>tr-0.1)&(d.t<tr)]; w=d[(d.t>=tr)&(d.t<tr+3)]; base=d[(d.t>tr-0.6)&(d.t<tr)]
    rise=w.beta.abs().max()-0  # peak during reversal
    return dict(case=n,aoa=round(pre.alpha.mean(),1),pS=round(abs(pre.pS.mean())),pred=round(pre.twoAxisPred.mean(),1),real=round(max(0,rise-base.beta.abs().max()),1),betaPre=round(base.beta.abs().max(),1),aR=round(pre.aAxR.mean()),aY=round(pre.aAxY.mean()),SR=round(pre.tAxR.mean()),SY=round(pre.tAxY.mean()))
with ThreadPoolExecutor(2) as ex: rows=list(ex.map(run,C))
r=pd.DataFrame(rows); r['ratio']=(r.real/r.pred.clip(lower=0.1)).round(1); print(tag,' '.join(sets)); print(r.to_string(index=False))
