import sys,numpy as np,pandas as pd
sys.path.insert(0,'.'); from s48 import sets
def post(tag):
    o=[]
    for lg,ts in sets.items():
        for t in ts:
            f=pd.read_csv(f'score/id_{tag}/{lg}_{t}_free.csv'); w=f[f.t>=t].reset_index(drop=True)
            if (np.abs(w.sr)>0.1).mean()>0.5: continue
            s0=np.sign(w.alpha_b[0]); brk=(s0*w.sp>-0.3)|(np.abs(w.sr-w.sr[0])>0.2); ie=np.argmax(brk.values) if brk.any() else len(w)-1
            a=w.alpha_b.values; pS=np.abs(w.pS_b.values); dt=np.median(np.diff(w.t))
            ia=np.argmax(np.abs(a)<10) if (np.abs(a)<10).any() else None
            if ia is None or ia>ie: continue
            stop=np.argmax(pS<2) if (pS<2).any() else len(w)-1
            dec=-np.gradient(np.convolve(pS,np.ones(5)/5,'same'),dt)
            o.append(dict(k=f'{lg}_{t}',tstop=w.t[stop]-t,after=w.t[stop]-w.t[ia],decmax=dec[:stop+1].max() if stop>0 else 0,aStop=abs(a[stop])))
    return pd.DataFrame(o).set_index('k')
for tag in sys.argv[1:]:
    d=post(tag); print(tag,len(d),' '.join(f"{c} {d[c].median():.2f}/{d[c].quantile(.9):.2f}/{d[c].max():.2f}" for c in d.columns))
