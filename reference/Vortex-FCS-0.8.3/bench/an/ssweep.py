# stop / reversal sweep for the stock-jet benches (ifly mode). usage: BENCH_DIR=... BENCH_DATA=... ssweep.py <tag> [Name=value ...]
import subprocess, os, sys, itertools, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
S=os.environ.get('AN_OUT','/home/claude/work/anout'); B=os.environ['BENCH_DIR']
tag=sys.argv[1]; sets=sys.argv[2:]
out=S+'/sw/'+tag; os.makedirs(out,exist_ok=True)
IAS=[int(x) for x in os.environ.get('SW_IAS','300,400,500,650').split(',')]; PULL=[float(x) for x in os.environ.get('SW_PULL','0.25,0.4,0.55,0.7').split(',')]
ALT=os.environ.get('SW_ALT','1500'); THR=os.environ.get('SW_THR','1')
T_ROLL=1.2; T_REV=2.1
def run(case):
    ias,pull,rev=case
    csv=f"{out}/i{ias}_p{pull}_{'rev' if rev else 'stop'}.csv"
    if os.path.exists(csv): return csv
    prof=f"0:0,0.1:{pull},7:{pull}"
    rprof=f"0:0,{T_ROLL}:0,{T_ROLL+0.05}:1,{T_REV}:1,{T_REV+0.05}:{-1 if rev else 0},{T_REV+1.2}:{-1 if rev else 0},{T_REV+1.25}:0,7:0"
    cmd=['dotnet',B+'/bin/bench.dll','ifly',f'ias={ias}',f'alt={ALT}',f'thr={THR}','T=4.5',f'pitch={prof}',f'roll={rprof}',f'fcslog={csv}']+sets
    r=subprocess.run(cmd,cwd=B,env=dict(os.environ),capture_output=True,text=True)
    if not os.path.exists(csv): print("FAILED",case,r.stderr[-400:])
    return csv
cases=list(itertools.product(IAS,PULL,[0,1]))
with ThreadPoolExecutor(4) as ex: files=list(ex.map(run,cases))
rows=[]
for (ias,pull,rev),f in zip(cases,files):
    d=pd.read_csv(f); d['pS']=d.p*np.cos(np.radians(d.alpha))+d.r*np.sin(np.radians(d.alpha))
    roll=d[(d.t>T_ROLL+0.3)&(d.t<T_REV)]; ev=d[(d.t>=T_REV)&(d.t<T_REV+1.3)]; entry=d[(d.t>=T_ROLL)&(d.t<T_REV)]
    a0=d[(d.t>T_ROLL-0.1)&(d.t<T_ROLL)].alpha.mean()
    p0=roll.pS.iloc[-1]; pmean=roll.p.abs().mean()
    tr=np.nan
    if rev:
        s=ev[ev.p<0.5*abs(p0)*np.sign(p0)] if p0>0 else ev[ev.p>0.5*p0]
        e=ev[ev.p< -0.5*abs(p0)] if p0>0 else ev[ev.p>0.5*abs(p0)]
        if len(s) and len(e): tr=e.t.iloc[0]-s.t.iloc[0]
    else:
        e=ev[ev.p.abs()<0.1*abs(p0)]
        if len(e): tr=e.t.iloc[0]-T_REV
    rows.append(dict(ias=ias,pull=pull,rev=rev,alpha=round(a0,1),pmean=round(pmean),p0=round(p0),betaEntry=round(entry.beta.abs().max(),1),betaEv=round(ev.beta.abs().max(),1),tRev=round(tr,2) if tr==tr else np.nan,alphaEv=round(ev.alpha.max(),1),ysat=round(ev.yawSat.max(),2)))
r=pd.DataFrame(rows); r.to_csv(out+'/summary.csv',index=False)
pd.set_option('display.width',250); pd.set_option('display.max_rows',200)
print(tag, ' '.join(sets)); print(r.to_string(index=False))
for rv in [0,1]:
    x=r[r.rev==rv]; print(f"{'reversal' if rv else 'stop'}: betaEv worst {x.betaEv.max():.1f} mean {x.betaEv.mean():.2f} | entry beta mean {x.betaEntry.mean():.2f} | mean roll {x.pmean.mean():.0f} | tRev mean {x.tRev.mean():.2f}")
