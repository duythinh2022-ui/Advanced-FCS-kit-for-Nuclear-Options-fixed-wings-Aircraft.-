#!/usr/bin/env python3
# s33.py tagA tagB ...: metrics on the 33 planned 2.35.x pushes (id_<tag> dirs). pinned: command dip, ramp roughness; free: |beta| peak, t to AoA<10, nose-down rate dip
import sys, numpy as np, pandas as pd
sys.path.insert(0,'/home/claude/w'); from hitch2 import dip
sets={'223711':[31.45,39.17,54.35,61.77,69.65,74.02,87.15,97.12,112.96],'225204':[43.15,84.42,93.31],
 '231000':[31.68,43.77,47.3,59.78,70.4,86.74,97.21,118.28,124.3,142.06,177.05,183.47,223.72],'232903':[55.4,59.38,65.02,79.92,88.37,136.13],'233737':[47.17,50.62]}
def m(tag):
    out=[]
    for lg,ts in sets.items():
        for t in ts:
            p=pd.read_csv(f'/home/claude/w/score/id_{tag}/{lg}_{t}_pin.csv'); f=pd.read_csv(f'/home/claude/w/score/id_{tag}/{lg}_{t}_free.csv')
            w=f[f.t>=t]; s=np.sign(w.alpha_b.values[0]); q=-s*w.q_b.values
            m_=(np.abs(w.alpha_b.values)>8)&(s*w.sp.values<-0.3)
            x=q[m_]; qd=float(np.max(np.minimum(np.maximum.accumulate(x)-x,np.maximum.accumulate(x[::-1])[::-1]-x))) if len(x)>5 else 0.
            tz=w.t[np.abs(w.alpha_b)<10]
            held=float((np.abs(p[p.t>=t].sr)>0.1).mean())
            out.append(dict(k=f'{lg}_{t}',held=held>0.5,cdip=dip(p.t,p.q_cmd,p.alpha_b,p.sp,t),fbeta=float(w.beta_b.abs().max()),t10=float(tz.iloc[0]-t) if len(tz) else np.nan,fqd=qd,qmax=float(np.max(q[m_])) if m_.any() else 0.))
    return pd.DataFrame(out).set_index('k')
tags=sys.argv[1:]; R={t:m(t) for t in tags}
for t in tags:
    d=R[t]
    for nm,sel in [('all',d.index==d.index),('released',~d.held),('held',d.held)]:
        x=d[sel]; print(f"{t:14s} {nm:9s} n={len(x):2d} " + "  ".join(f"{c} {x[c].median():5.2f}/{x[c].quantile(.9):5.2f}/{x[c].max():5.1f}" for c in ['cdip','fbeta','t10','fqd','qmax']))
if len(tags)>1:
    a,b=R[tags[0]],R[tags[1]]
    for c in ['cdip','fbeta','t10','fqd']:
        dd=b[c]-a[c]; th=0.1 if c=='t10' else 1.0
        print(f"  {tags[1]} vs {tags[0]} {c:6s} better {int((dd<-th).sum()):2d} worse {int((dd>th).sum()):2d} median {dd.median():+.2f}")
