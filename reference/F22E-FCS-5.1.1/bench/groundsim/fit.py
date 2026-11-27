import sys, numpy as np, pandas as pd, itertools
sys.path.insert(0,".")
from gsim import Jet, Sim
f="/mnt/user-data/attachments/552530bf-d4b0-5969-978f-ef60367182d4/67956193-Ifrit_FCS_20261027_105534.csv"
d=pd.read_csv(f,usecols=["t","tas","r","sy","gsNws"])
def replay(kz,bf,t0,t1):
    s=d[(d.t>=t0)&(d.t<=t1)].reset_index(drop=True)
    j=Jet("ifrit",25800,7.07,bf,4.2,0.7,0.8,(-1,1),kz=kz)
    sim=Sim(j,s.tas.iloc[0]); sim.r=s.r.iloc[0]*np.pi/180
    tt=np.arange(t0,t1,0.02); out=[]
    for t in tt:
        i=min(np.searchsorted(s.t.values,t),len(s)-1)
        sim.V=max(s.tas.iloc[i],0.5)
        sim.step(s.gsNws.iloc[i]/45.0, s.sy.iloc[i])
        out.append(sim.r*180/np.pi)
    rl=np.interp(tt,s.t.values,s.r.values)
    return tt,np.array(out),rl
segs=[(8,25),(40,61)]
best=[]
for kz,bf in itertools.product([4,4.5,5,5.5,6],[0.16,0.2,0.24,0.28,0.32]):
    e=0
    for a,b in segs:
        tt,rs,rl=replay(kz,bf,a,b); e+=np.sqrt(np.mean((rs-rl)**2))
    best.append((e,kz,bf))
best.sort(); print(best[:6])
e,kz,bf=best[0]
for a,b in segs:
    tt,rs,rl=replay(kz,bf,a,b)
    print("seg",a,b,"rms",np.sqrt(np.mean((rs-rl)**2)).round(2))
    for k in range(0,len(tt),25): print(f"  t {tt[k]:.1f} r_log {rl[k]:6.1f} r_sim {rs[k]:6.1f}")
