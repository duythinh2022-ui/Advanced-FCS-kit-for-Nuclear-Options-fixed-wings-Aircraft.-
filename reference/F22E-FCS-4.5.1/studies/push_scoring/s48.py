#!/usr/bin/env python3
# s48.py tagA tagB ...: the 33 planned 2.35.x pushes + 15 of the 4 Nov 2.35.3 flights; pinned command dip; free: |beta| peak, beta swing,
# t to AoA<10, peak yaw-demand drain d(pS sin a)/dt, peak roll-ref decel, velocity roll left at AoA 10
import sys, numpy as np, pandas as pd
sys.path.insert(0,'/home/claude/w'); from hitch2 import dip
sets={'223711':[31.45,39.17,54.35,61.77,69.65,74.02,87.15,97.12,112.96],'225204':[43.15,84.42,93.31],
 '231000':[31.68,43.77,47.3,59.78,70.4,86.74,97.21,118.28,124.3,142.06,177.05,183.47,223.72],'232903':[55.4,59.38,65.02,79.92,88.37,136.13],'233737':[47.17,50.62],
 '20261104_011013':[30.68,40.95,59.12,63.63,76.53,90.14,117.06,128.21,132.26],'20261104_011409':[36.62,95.54,115.38,155.01,170.0,414.66]}
def sm(x,n=5): return np.convolve(x,np.ones(n)/n,mode='same')
def m(tag):
    out=[]
    for lg,ts in sets.items():
        for t in ts:
            p=pd.read_csv(f'/home/claude/w/score/id_{tag}/{lg}_{t}_pin.csv'); f=pd.read_csv(f'/home/claude/w/score/id_{tag}/{lg}_{t}_free.csv')
            w=f[f.t>=t].reset_index(drop=True); dt=np.median(np.diff(w.t))
            a=w.alpha_b.values; ia=np.argmax(np.abs(a)<10) if (np.abs(a)<10).any() else len(a)-1
            Y=np.abs(w.pS_b.values)*np.abs(np.sin(np.radians(a)))
            yd=-np.gradient(sm(Y),dt); pd_=-np.gradient(sm(np.abs(w.pS_b.values)),dt)
            s0=np.sign(a[0]); brk=(s0*w.sp.values>-0.3)|(np.abs(w.sr.values-w.sr.values[0])>0.2)
            ie=np.argmax(brk) if brk.any() else len(a)-1
            if ia>ie: ia=len(a)-1; t10nan=True
            else: t10nan=False
            seg=slice(0,max(min(ia,ie),5))
            pV=w.p_b*np.cos(np.radians(a))+w.r_b*np.sin(np.radians(a))
            held=float((np.abs(p[p.t>=t].sr)>0.1).mean())>0.5
            out.append(dict(k=f'{lg}_{t}',held=held,cdip=dip(p.t,p.q_cmd,p.alpha_b,p.sp,t),fbeta=float(w.beta_b.abs().max()),
              bsw=float(w.beta_b.max()-w.beta_b.min()),t10=np.nan if t10nan else float(w.t[ia]-t),ydmax=float(np.max(yd[seg])),pdec=float(np.max(pd_[seg])),
              pV10=np.nan if t10nan else float(abs(pV[ia]))))
    return pd.DataFrame(out).set_index('k')
if __name__!="__main__": tags=[]
else: tags=sys.argv[1:]
R={t:m(t) for t in tags}
C=['cdip','fbeta','bsw','t10','ydmax','pdec','pV10']
for t in tags:
    d=R[t]
    for nm,sel in [('all',d.index==d.index),('released',~d.held),('held',d.held)]:
        x=d[sel]; print(f"{t:6s} {nm:8s} n={len(x):2d} " + " ".join(f"{c} {x[c].median():5.1f}/{x[c].quantile(.9):5.1f}/{x[c].max():5.1f}" for c in C))
for tb in tags[1:]:
    a,b=R[tags[0]],R[tb]
    print(f"  {tb} vs {tags[0]}: "+"  ".join(f"{c} +{int(((b[c]-a[c])<-(0.1 if c=='t10' else 1)).sum())}/-{int(((b[c]-a[c])>(0.1 if c=='t10' else 1)).sum())}" for c in ['cdip','fbeta','bsw','t10']))
if '-v' in sys.argv[0:0]: pass
if __name__=='__main__' and len(tags)>=2:
    a,b=R[tags[0]],R[tags[-1]]
    j=a.join(b,lsuffix='_a',rsuffix='_b')
    print(j[[f'{c}_{s}' for c in ['fbeta','bsw','t10','pdec','pV10'] for s in 'ab']+['held_a']].round(2).to_string())
