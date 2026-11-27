# saturated-braking samples near the limit: what the jet gets vs what the plan / live estimate say
import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *; import sys  # kit 5.0: paths relative to this tree
import pandas as pd, numpy as np, json, sys
tag, cg = sys.argv[1], sys.argv[2]
W=json.load(open(OV+"/windows.json")); S=[]
for w in W:
    d=pd.read_csv(f"{OV}/out/{tag}_{cg}/w{w['id']:02d}.csv")
    t=d.t.values; q=pd.Series(d.q_b.values).rolling(6,center=True,min_periods=1).mean().values
    d['qd']=np.gradient(q,t); ip=int(d.alpha_b.values.argmax())
    x=d.iloc[:ip+1]; x=x[(x.alpha_b>48)&(x.unmetY_b<-2)&(x.q_b>0)]
    x=x.assign(id=w['id']); S.append(x)
S=pd.concat(S); S['ach']=-S.qd
S['roll']=np.where(S.sr.abs()>0.5,'roll','noroll')
S['stabSplit']=(S.stabL_b-S.stabR_b).abs()
g=S.groupby('roll')
print(f"CG {cg}, samples AoA>48, pitch nose-down unmet >2 deg/s^2, AoA rising:")
print(g.agg(n=('ach','size'),windows=('id','nunique'),ach=('ach','median'),aDn=('aDn_b','median'),plan=('aBrakePlan_b','median'),aNet=('planANet','median'),take=('planRollTake','median'),coup=('planCoupArr','median'),tgt=('aBrakeTgt_b','median'),split=('stabSplit','median')).round(1).to_string())
for r,x in g:
    e=(x.aBrakePlan_b-x.ach)/x.ach.clip(lower=1); en=(x.planANet-x.ach)/x.ach.clip(lower=1)
    print(r,"plan/ach-1 median %+.0f%%, aNet/ach-1 %+.0f%%, aDn/ach-1 %+.0f%%"%(100*e.median(),100*en.median(),100*((x.aDn_b-x.ach)/x.ach.clip(lower=1)).median()))
