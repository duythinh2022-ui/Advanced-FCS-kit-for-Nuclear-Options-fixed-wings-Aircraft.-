# offline check of the measurement-anchored capability at the target, against achieved braking over the last 5 deg
import pandas as pd, numpy as np, json, sys
tag=sys.argv[1]; W=json.load(open("windows.json")); R=[]
for w in W:
    d=pd.read_csv(f"out/{tag}/w{w['id']:02d}.csv")
    t=d.t.values; a=d.alpha_b.values; q=d.q_b.values
    qd=np.gradient(pd.Series(q).rolling(6,center=True,min_periods=1).mean().values,t)
    ip=int(a.argmax()); pk=a[ip]
    if pk<47: continue
    seg=[i for i in range(ip+1) if a[i]>=pk-5 and q[i]>d.capBoundU_b.values[i] and qd[i]<0]
    if len(seg)<3: continue
    ach=-qd[seg].mean()
    rec=dict(id=w['id'],pk=pk,roll=np.abs(d.sr.values[:ip+1]).max(),ach=ach)
    for c in (40,45):
        ix=[i for i in range(1,ip+1) if a[i-1]<c<=a[i]]
        if not ix: continue
        i=ix[-1]; r=d.iloc[i]
        ratio=r.planANet/max(1,r.dnFullHere)
        rec[f'anc{c}']=max(4,(r.aDn_b+r.coupNow)*ratio - r.planCoupArr)
        rec[f'ancNoSw{c}']=max(4,r.aDn_b*ratio)
        rec[f'model{c}']=r.planANet; rec[f'old{c}']=r.aBrakeTgt_b
    R.append(rec)
R=pd.DataFrame(R)
for k in ['anc','ancNoSw','model','old']:
    for c in (40,45):
        x=R[[f'{k}{c}','ach']].dropna(); e=(x[f'{k}{c}']-x.ach)/x.ach
        print(f"{tag} {k:8s}@{c}: n={len(x)} median {100*e.median():+4.0f}%  IQR {100*e.quantile(.25):+4.0f}..{100*e.quantile(.75):+4.0f}%  >1.5x: {(x[f'{k}{c}']>1.5*x.ach).sum()}  <0.67x: {(x[f'{k}{c}']<0.67*x.ach).sum()}")
