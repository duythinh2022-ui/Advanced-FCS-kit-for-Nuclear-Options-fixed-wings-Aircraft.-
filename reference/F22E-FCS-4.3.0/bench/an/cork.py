# positive-AoA corkscrew series: pull + held full roll for several seconds, scored for smoothness.
# usage: cork.py <tag> [Name=value ...]   (BENCH_DIR env)
import subprocess, os, sys, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
from scipy.signal import savgol_filter
S='/tmp/claude-0/-home-claude/c3114abd-20fc-5984-be11-c25d56f03f2d/scratchpad'; B=os.environ.get('BENCH_DIR',S+'/src/F22E_FCS_2.5.16_src/bench')
tag=sys.argv[1]; sets=sys.argv[2:]; out=S+'/cork/'+tag; os.makedirs(out,exist_ok=True)
HOLD=6.0  # s of roll held
CASES=[]
# (name, ias, pitch stick, roll start after pull start, throttle)
for ias in [250,350,450,550]:
    for ps in [0.6,0.8,1.0]:
        CASES.append((f'pull{ps}+roll@0.3 {ias}',ias,ps,0.3,1.0))
for ias in [250,350,450]:
    CASES.append((f'pull1.0+roll@0 {ias}',ias,1.0,0.0,1.0))
    CASES.append((f'roll-then-pull1.0 {ias}',ias,1.0,-1.0,1.0))   # roll first, pull 0.8 s later
for ias in [350,450]:
    CASES.append((f'pull0.8+roll@0.3 {ias} thr0.6',ias,0.8,0.3,0.6))
def profs(ps,rd):
    if rd>=0:
        prof=f"0:0,0.05:{ps},9:{ps}"; rprof=f"0:0,{rd:.2f}:0,{rd+0.05:.2f}:1,{rd+HOLD:.2f}:1,{rd+HOLD+0.05:.2f}:0,9:0"; w=(rd+0.5,rd+HOLD)
    else:
        prof=f"0:0,0.8:0,0.85:{ps},9:{ps}"; rprof=f"0:0,0.05:1,{HOLD+0.8:.2f}:1,{HOLD+0.85:.2f}:0,9:0"; w=(1.3,HOLD+0.8)
    return prof,rprof,w
def run(i):
    name,ias,ps,rd,thr=CASES[i]; csv=f"{out}/c{i}.csv"
    if os.path.exists(csv): return csv
    prof,rprof,w=profs(ps,rd)
    cmd=['dotnet',B+'/bin/bench.dll','one',f'iasKmh={ias}','alt=1500','trimT=1','pull=0','hold=0','roll=0','post=9.5',f'prof={prof}',f'rprof={rprof}',f'throttle={thr}',f'csv={csv}']+sets
    subprocess.run(cmd,cwd=B,env=dict(os.environ,BENCH_DATA=S+'/airframe_cal.json'),capture_output=True)
    return csv
def revs(x,th):
    # sign changes of x ignoring |x|<th
    s=np.sign(np.where(np.abs(x)<th,0,x)); s=s[s!=0]
    return int((np.diff(s)!=0).sum()) if len(s)>1 else 0
with ThreadPoolExecutor(3) as ex: files=list(ex.map(run,range(len(CASES))))
rows=[]
for (name,ias,ps,rd,thr),f in zip(CASES,files):
    d=pd.read_csv(f); prof,rprof,w=profs(ps,rd); s=d[(d.t>=w[0])&(d.t<=w[1])]
    if len(s)<30: rows.append(dict(case=name)); continue
    dt=1/60
    pdot=savgol_filter(s.p.values,9,2,deriv=1,delta=dt); qdot=savgol_filter(s.q.values,9,2,deriv=1,delta=dt)
    pSv=s.pS.values if 'pS' in s else s.p.values
    # settle: drop the first 1 s of the window for the steady metrics
    ss=s[s.t>=w[0]+1.0]
    pdot2=savgol_filter(ss.p.values,9,2,deriv=1,delta=dt); qdot2=savgol_filter(ss.q.values,9,2,deriv=1,delta=dt)
    rows.append(dict(case=name,
        aMean=round(ss.alpha.mean(),1),aStd=round(ss.alpha.std(),2),aPP=round(ss.alpha.max()-ss.alpha.min(),1),
        bPk=round(s.beta.abs().max(),1),bRms=round(np.sqrt((ss.beta**2).mean()),2),bRev=revs(ss.beta.values,1.0),
        pSMean=round(np.abs(ss.pS.values).mean()) if 'pS' in ss else np.nan,pSStd=round(ss.pS.std(),1) if 'pS' in ss else np.nan,
        pMean=round(np.abs(ss.p.values).mean()),pStd=round(ss.p.std(),1),pdotRms=round(np.sqrt((pdot2**2).mean())),
        qStd=round(ss.q.std(),1),qdotRms=round(np.sqrt((qdot2**2).mean())),qRev=revs(ss.q.values-ss.q.values.mean(),3.0),
        rStd=round(ss.r.std(),1),rudRev=revs(np.diff(ss.rudL.values),0.5) if 'rudL' in ss else np.nan,
        tgtStd=round(ss.pSTgt.std(),1) if 'pSTgt' in ss else np.nan,
        hold=round(ss.paceHold.abs().max(),0) if 'paceHold' in ss else np.nan,
        pTrade=round(ss.pullTrade.min(),2) if 'pullTrade' in ss else np.nan,
        sgp=round(ss.slipGovPull.min(),2) if 'slipGovPull' in ss else np.nan,
        ias1=round(ss.ias.iloc[-1])))
r=pd.DataFrame(rows); r.to_csv(out+'/summary.csv',index=False); pd.set_option('display.width',300); pd.set_option('display.max_columns',40)
print(tag,' '.join(sets)); print(r.to_string(index=False))
