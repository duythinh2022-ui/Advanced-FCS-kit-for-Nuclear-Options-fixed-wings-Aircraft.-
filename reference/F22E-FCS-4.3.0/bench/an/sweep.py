# stop / reversal sweep on the calibrated bench. usage: sweep.py <tag> [Name=value ...]
import subprocess, os, sys, itertools, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
S='/tmp/claude-0/-home-claude/c3114abd-20fc-5984-be11-c25d56f03f2d/scratchpad'
B=os.environ.get('BENCH_DIR',S+'/src/F22E_FCS_2.5.7_src/bench')
tag=sys.argv[1]; sets=sys.argv[2:]
out=S+'/sw/'+tag; os.makedirs(out,exist_ok=True)
IAS=[int(x) for x in os.environ.get('SW_IAS','450,565,700,850').split(',')]; PULL=[float(x) for x in os.environ.get('SW_PULL','0.25,0.35,0.45,0.55').split(',')]
T_ROLL=1.2; T_REV=2.1   # from the pull start
def run(case):
    ias,pull,rev=case
    csv=f"{out}/i{ias}_p{pull}_{'rev' if rev else 'stop'}.csv"
    if os.path.exists(csv): return csv
    prof=f"0:0,0.1:{pull},7:{pull}"
    rprof=f"0:0,{T_ROLL}:0,{T_ROLL+0.05}:1,{T_REV}:1,{T_REV+0.05}:{-1 if rev else 0},{T_REV+1.2}:{-1 if rev else 0},{T_REV+1.25}:0,7:0"
    cmd=['dotnet',B+'/bin/bench.dll','one',f'iasKmh={ias}','alt=1500','trimT=1','pull=0','hold=0','roll=0','post=4.5',f'prof={prof}',f'rprof={rprof}',f'csv={csv}']+sets
    subprocess.run(cmd,cwd=B,env=dict(os.environ,BENCH_DATA=S+'/airframe_cal.json'),capture_output=True)
    return csv
cases=list(itertools.product(IAS,PULL,[0,1]))
with ThreadPoolExecutor(2) as ex: files=list(ex.map(run,cases))
rows=[]
for (ias,pull,rev),f in zip(cases,files):
    d=pd.read_csv(f)   # t is relative to tRel = trimT+hold = 1.0 -> pull starts at t=0 (trimT=1,hold=0 => tRel=tPull)
    roll=d[(d.t>T_ROLL+0.3)&(d.t<T_REV)]; ev=d[(d.t>=T_REV)&(d.t<T_REV+1.3)]; entry=d[(d.t>=T_ROLL)&(d.t<T_REV)]
    a0=d[(d.t>T_ROLL-0.1)&(d.t<T_ROLL)].alpha.mean()
    p0=roll.pS.iloc[-1]; pmean=roll.p.abs().mean()
    # reversal time: p from +50% p0 to -50% p0 ; stop time: p to <10% 
    tr=np.nan
    if rev:
        s=ev[ev.p<0.5*abs(p0)*np.sign(p0)] if p0>0 else ev[ev.p>0.5*p0]
        e=ev[ev.p< -0.5*abs(p0)] if p0>0 else ev[ev.p>0.5*abs(p0)]
        if len(s) and len(e): tr=e.t.iloc[0]-s.t.iloc[0]
    else:
        e=ev[ev.p.abs()<0.1*abs(p0)]
        if len(e): tr=e.t.iloc[0]-T_REV
    dphi=abs(d[(d.t>=T_REV+1.0)].phi.iloc[0]-d[(d.t>=T_REV)].phi.iloc[0]); dphi=min(dphi,360-dphi)
    rows.append(dict(ias=ias,pull=pull,rev=rev,alpha=round(a0,1),pmean=round(pmean),p0=round(p0),betaEntry=round(entry.beta.abs().max(),1),betaEv=round(ev.beta.abs().max(),1),tRev=round(tr,2) if tr==tr else np.nan,dphi1s=round(dphi),alphaEv=round(ev.alpha.max(),1)))
r=pd.DataFrame(rows); r.to_csv(out+'/summary.csv',index=False)
pd.set_option('display.width',250); pd.set_option('display.max_rows',200)
print(tag, ' '.join(sets)); print(r.to_string(index=False))
for rv in [0,1]:
    x=r[r.rev==rv]; print(f"{'reversal' if rv else 'stop'}: betaEv worst {x.betaEv.max():.1f} mean {x.betaEv.mean():.2f} | entry beta mean {x.betaEntry.mean():.2f} | mean roll {x.pmean.mean():.0f} | tRev mean {x.tRev.mean():.2f} | dphi1s mean {x.dphi1s.mean():.0f}")
