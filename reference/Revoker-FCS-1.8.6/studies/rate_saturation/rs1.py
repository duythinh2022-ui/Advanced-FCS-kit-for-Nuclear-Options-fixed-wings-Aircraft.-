# one-line rate-saturation summary. usage: rs1.py <label> <csv> [t0 t1]
import sys, numpy as np, pandas as pd
from scipy.signal import butter, filtfilt
lab=sys.argv[1]; d=pd.read_csv(sys.argv[2])
if len(sys.argv)>=5: d=d[(d.t>=float(sys.argv[3]))&(d.t<=float(sys.argv[4]))]
d=d.reset_index(drop=True); dt=d.t.diff().median()
surf=[c[:-4] for c in d.columns if c.endswith('_now') and c[:-4]+'_cmd' in d.columns]
b,a=butter(2,[0.1*2*dt,min(0.99,10*2*dt)],'band')
tr=[];lim=[];bn=[];ld=[]
for s in surf:
    v=np.abs(np.gradient(d[s+'_now'].values,d.t.values)); tr.append(v.mean()); lim.append(100*(v>=0.9*98).mean())
    bn.append(filtfilt(b,a,d[s+'_now'].values).std()); ld.append(np.percentile((d[s+'_cmd']-d[s+'_now']).abs(),95))
f=lambda c: filtfilt(b,a,d[c].values).std() if c in d else float('nan')
tg='pSTarget' if 'pSTarget' in d else 'pSTgt'
print(f"{lab:34s} travel {np.mean(tr):5.1f} deg/s (max {np.max(tr):5.1f}) atLimit {np.mean(lim):4.1f}% (max {np.max(lim):4.1f}) band {np.mean(bn):4.2f} lead95 {np.mean(ld):4.1f} | nuR {f('nuR'):5.1f} nuY {f('nuY'):4.1f} cap {f('rateCap'):5.2f} tgt {f(tg):4.2f} pband {f('p'):4.2f} | p {d.p.abs().mean():5.1f} tgt {d[tg].abs().mean():5.1f} cap {d.rateCap.clip(upper=200).mean():5.1f} aoa {d.alpha.mean():4.1f} |b|max {d.beta.abs().max():4.1f} b95 {d.beta.abs().quantile(.95):4.1f}")
