# roll stop / reversal sweep on the bench: python3 sweep.py <benchdir> <tag> [key=val ...]
import subprocess, sys, os, itertools, pandas as pd, numpy as np
from concurrent.futures import ThreadPoolExecutor
B=sys.argv[1]; tag=sys.argv[2]; extra=sys.argv[3:]
S="/tmp/claude-0/-home-claude/91b81c1e-955b-555c-b741-a538fa553687/scratchpad"
R=f"{S}/runs/{tag}"; os.makedirs(R,exist_ok=True)
env=dict(os.environ, BENCH_DATA=f"{S}/ex/bench_airframe.json")
IAS=[int(x) for x in os.environ.get("IAS","350,450,565,650,750").split(",")]
SP=[float(x) for x in os.environ.get("SP","0.5,0.6,0.7,0.8,0.9").split(",")]
RS=[float(x) for x in os.environ.get("RS","0.5,1.0").split(",")]
EV=os.environ.get("EV","stop,rev").split(",")
HOLD=float(os.environ.get("HOLD","0.8"))
def job(c):
    ias,sp,rs,ev=c
    t1=2.0; t2=t1+0.1; t3=t2+HOLD; 
    if ev=="stop": rp=f"0:0,{t1}:0,{t2}:{-rs},{t3}:{-rs},{t3+0.15}:0,12:0"
    else: rp=f"0:0,{t1}:0,{t2}:{-rs},{t3}:{-rs},{t3+0.15}:{rs},{t3+0.15+HOLD}:{rs},{t3+0.3+HOLD}:0,12:0"
    f=f"{R}/i{ias}_s{sp}_r{rs}_{ev}.csv"
    cmd=["dotnet",f"{B}/bin/bench.dll","one",f"iasKmh={ias}","alt=200","throttle=1","trimT=2","pull=0","hold=0",f"prof=0:0,0.4:{sp},30:{sp}",f"rprof={rp}","post=6",f"csv={f}"]+extra
    subprocess.run(cmd,env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,timeout=300)
    try: d=pd.read_csv(f)
    except Exception as e: return None
    rel=t3+0.075
    pre=d[(d.t>t3-0.15)&(d.t<=t3)]
    roll=d[(d.t>t2)&(d.t<=t3)]
    post=d[(d.t>t3)&(d.t<t3+(2.5 if ev=="stop" else 2.5+HOLD))]
    if len(pre)==0 or len(post)==0: return None
    k=post.beta.abs().idxmax()
    kr=roll.beta.abs().idxmax()
    return dict(ias=ias,sp=sp,rs=rs,ev=ev,alpha=pre.alpha.mean(),nz=pre.nz.mean(),p=pre.p.abs().max(),r=pre.r.abs().max(),pS=roll.pS.abs().max(),
                bRoll=roll.beta.abs().max(),bStop=abs(d.beta[k]),aB=pre.stopSlipAB.mean(),aRoll=pre.aRoll.mean(),latBrake=d[(d.t>t3+0.05)&(d.t<t3+0.3)].latAcc.mean(),lim=pre.stopSlipLim.mean(),rud=pre.rudL.abs().max(),
                phi1=abs(d[(d.t<=t2+1.0)].phi.iloc[-1]-d[(d.t<=t2)].phi.iloc[-1]))
cases=list(itertools.product(IAS,SP,RS,EV))
with ThreadPoolExecutor(8) as ex: res=[r for r in ex.map(job,cases) if r]
df=pd.DataFrame(res); df.to_csv(f"{R}/summary.csv",index=False)
pd.set_option("display.width",250); pd.set_option("display.max_rows",500)
print(df.to_string(index=False,float_format=lambda x:f"{x:.1f}"))
