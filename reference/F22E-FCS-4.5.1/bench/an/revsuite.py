# reversal suite: time to zero / 90 % and peak sideslip for full-stick roll reversals. revsuite.py <benchdir> <tag> [Name=value ...]
import sys, os, subprocess, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
B=sys.argv[1]; tag=sys.argv[2]; sets=sys.argv[3:]; O=os.environ.get('REG_S','/tmp')+f'/revs/{tag}'; os.makedirs(O,exist_ok=True)
env=dict(os.environ)  # BENCH_DATA from the environment (suite.sh sets it)
C=[]
for ias in (450,556,650):  # full pull + roll, reversed as the AoA climbs (the 23:37 log, 122 s)
    C.append((f'pull+roll rev@0.7 {ias}',ias,'0:0,0.05:1,7:1','0:0,0.05:1,0.7:1,0.75:-1,5:-1',0.75))
for ias in (400,556,700):  # held half pull, roll reversal at moderate AoA
    C.append((f'0.5pull roll rev {ias}',ias,'0:0,0.05:0.5,7:0.5','0:0,1:0,1.05:1,3:1,3.05:-1,6:-1',3.05))
for ias in (300,500):      # held full aft, reversal at high AoA
    C.append((f'fullaft roll rev {ias}',ias,'0:0,0.05:1,8:1','0:0,0.05:1,3:1,3.05:-1,6:-1',3.05))
for ias in (500,800):      # 1 g
    C.append((f'1g roll rev {ias}',ias,'0:0,8:0','0:0,0.05:1,1.5:1,1.55:-1,4:-1',1.55))
for ias in (160,300):      # falling leaf
    C.append((f'leaf rev {ias}',ias,'0:0,0.8:0,0.85:1,17:1','0:0,4:0,4.05:1,9:1,9.05:-1,15:-1',9.05))
def run(c):
    n,ias,prof,rprof,tr=c; f=f'{O}/{n.replace(" ","_")}.csv'
    if not os.path.exists(f):
        subprocess.run(['dotnet',B+'/bin/bench.dll','one',f'iasKmh={ias}','alt=1500','trimT=1','pull=0','hold=0','roll=0',f'post={tr+5}','throttle=1',f'prof={prof}',f'rprof={rprof}',f'csv={f}']+sets,cwd=B,env=env,capture_output=True)
    d=pd.read_csv(f); pre=d[(d.t>tr-0.2)&(d.t<tr)]; s0=np.sign(pre.pS.mean()); w=d[(d.t>=tr)&(d.t<tr+4.5)]
    z=w[w.pS*s0<=0]; tz=z.t.iloc[0]-tr if len(z) else np.nan
    pk=w.pS.abs().max() if not len(z) else w[w.t>tr+tz].pS.abs().max()
    r9=w[(w.pS*s0<0)&(w.pS.abs()>=0.9*pk)]; t9=r9.t.iloc[0]-tr if len(r9) else np.nan
    st=d[(d.t>tr-1.5)&(d.t<tr)]
    return dict(case=n,aoa=round(pre.alpha.mean(),1),pS0=round(abs(pre.pS.mean())),tZero=round(tz,2),t90=round(t9,2),newPk=round(pk),beta=round(w.beta.abs().max(),1),betaPre=round(st.beta.abs().max(),1))
with ThreadPoolExecutor(2) as ex: rows=list(ex.map(run,C))
r=pd.DataFrame(rows); r.to_csv(f'{O}/summary.csv',index=False); print(tag,' '.join(sets)); print(r.to_string(index=False))
