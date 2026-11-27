# F-22E replay summary (bench _b vs game _g columns). usage: frs.py <label> <csv>
import sys, numpy as np, pandas as pd
from scipy.signal import butter, filtfilt
lab=sys.argv[1]; d=pd.read_csv(sys.argv[2]); dt=d.t.diff().median()
b,a=butter(2,[0.1*2*dt,min(0.99,10*2*dt)],'band'); bp=lambda x: filtfilt(b,a,np.asarray(x,float)).std()
def trav(sfx):
    cs=[c for c in ['stabL','stabR','rudL','rudR','ailL','flapL'] if c+sfx in d]
    v=[np.abs(np.gradient(d[c+sfx].values,d.t.values)) for c in cs]
    return np.mean([x.mean() for x in v]), np.mean([100*(x>=0.9*98).mean() for x in v]), np.mean([bp(d[c+sfx]) for c in cs])
tb=trav('_b'); tg=trav('_g')
print(f"{lab:26s} bench travel {tb[0]:5.1f} atLimit {tb[1]:4.1f}% band {tb[2]:4.2f} | game travel {tg[0]:5.1f} atLimit {tg[1]:4.1f}% band {tg[2]:4.2f} | tgt band {bp(d.pSTgt):5.2f} capZe band {bp(d.capZeP_b):5.1f}/{bp(d.capZeN_b):5.1f} | p_b {d.p_b.abs().mean():5.1f} p_g {d.p_g.abs().mean():5.1f} tgt {d.pSTgt.abs().mean():5.1f} | beta_b max {d.beta_b.abs().max():4.1f} g {d.beta_g.abs().max():4.1f} aoa_b {d.alpha_b.mean():4.1f} g {d.alpha_g.mean():4.1f}")
