import subprocess, os, pandas as pd, numpy as np, sys
from concurrent.futures import ThreadPoolExecutor
S="/tmp/claude-0/-home-claude/91b81c1e-955b-555c-b741-a538fa553687/scratchpad"; B="/home/claude/w/src/F22E_FCS_2.4.18_src/bench"
env=dict(os.environ,BENCH_DATA=f"{S}/ex/bench_airframe.json"); R=f"{S}/runs/ab"; os.makedirs(R,exist_ok=True)
cfgs={"16":["RollStopSlipGov=false","StopNoChase=false","YawSlewLead=false"],"18":[],"18noLead":["YawSlewLead=false"],"18noGov":["RollStopSlipGov=false"],"18NC":["RollStopSlipGov=false","YawSlewLead=false"]}
sc={"A":"pull=1 hold=4 rollFromPull=0 roll=1 rollT=4 post=0.3 throttle=1",
    "B":"hold=5 prof=0:0,1:0,1.02:1,5:1 rprof=0:1,5:1 post=0.3 throttle=1",
    "Bstop":"hold=5 prof=0:0,1:0,1.02:1,6:1 rprof=0:1,3:1,3.15:0,6:0 post=0.3 throttle=1",
    "Brev":"hold=5 prof=0:0,1:0,1.02:1,6:1 rprof=0:1,3:1,3.15:-1,4.5:-1,4.65:0,6:0 post=0.3 throttle=1"}
jobs=[(c,s,i) for c in cfgs for s in sc for i in (250,320,400,500,600)]
def run(j):
    c,s,i=j; f=f"{R}/{c}_{s}_{i}.csv"
    subprocess.run(["dotnet",f"{B}/bin/bench.dll","one",f"iasKmh={i}",*sc[s].split(),*cfgs[c],f"csv={f}"],env=env,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    d=pd.read_csv(f); tp=d.t[d.sp>0.5].iloc[0]; w=d[(d.t>=tp)&(d.t<=tp+3)]; w2=w[w.t<=tp+2]
    t30=w.t[w.alpha>=30]; t30=(t30.iloc[0]-tp) if len(t30) else np.nan
    a2=np.radians(w2.alpha)
    return dict(cfg=c,sc=s,ias=i,betaMax=d[d.t>=tp].beta.abs().max(),a2=w2.alpha.iloc[-1],t30=t30,pS2=(w2.p*np.cos(a2)+w2.r*np.sin(a2)).abs().mean(),phiEnd=abs(d.phi.iloc[-1]))
with ThreadPoolExecutor(4) as ex: r=pd.DataFrame(list(ex.map(run,jobs)))
for s in sc:
    print("scenario",s); print(r[r.sc==s].pivot_table(index="ias",columns="cfg",values=["betaMax","a2"]).round(1).to_string())
