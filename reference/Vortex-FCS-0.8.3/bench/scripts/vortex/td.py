# touchdown analysis: td.py <csv> ...   contact = main gear uncompressed height (rest height + static compression)
import pandas as pd,numpy as np,sys,re,os
def an(f, stat=None):
    d=pd.read_csv(f); g=d[d.t<5].alt.mean()
    if stat is None:
        log=open(f.replace('.csv','.txt')).read(); m=re.search(r'static was about ([\d.]+)',log); stat=float(m.group(1)) if m else 0.145
    hc=g+stat; w=d[d.t>20]
    c=w[w.alt<hc]
    if not len(c): return f"{os.path.basename(f):14s} no touchdown"
    i0=c.index[0]; t0=d.loc[i0,'t']; a=d[d.t>=t0]; v0=d.loc[i0-1,'vs']
    off=a[a.alt>hc+0.002]; nb=0; tb=0.0
    # count separate airborne intervals after first contact
    air=(a.alt>hc+0.002).values; nb=int(np.sum(air[1:]&~air[:-1])); tb=air.sum()*(d.t.iloc[1]-d.t.iloc[0])
    dt=d.t.iloc[1]-d.t.iloc[0]
    acc=np.gradient(a.vs.values,dt); 
    # time from 3 m above contact to contact
    hi=w[(w.alt<hc+3)]; t3=t0-hi.t.iloc[0] if len(hi) else float('nan')
    return (f"{os.path.basename(f):14s} contact t {t0:6.2f} at {v0:+.2f} m/s (thrEng {d.loc[i0-1,'thrEng']:.2f}), stroke max {hc-a.alt.min():.3f} m, "
            f"wheels off again {nb}x for {tb:.2f} s, highest {max(0,(a.alt-hc).max()):.3f} m, peak g {acc.max()/9.81:+.2f}, last 3 m took {t3:.2f} s, settled vs<0.05 at +{(a[(a.vs.abs()>0.05)].t.max()-t0):.2f} s")
for f in sys.argv[1:]: print(an(f))
