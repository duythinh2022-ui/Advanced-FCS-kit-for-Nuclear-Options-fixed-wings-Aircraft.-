import sys, pandas as pd, numpy as np
def dpsi(w):
    p=np.unwrap(np.radians(w.psi.values)); p=np.degrees(p-p[0]); return p.min(),p.max()
for f in sys.argv[1:]:
    d=pd.read_csv(f+'/H.csv'); out=f'{f[-22:]:22s}'
    for t0,t1 in ((20,40),(40,50),(109.9,125)):
        w=d[(d.t>=t0)&(d.t<t1)]; a,b=dpsi(w); sat=(w.postIn.abs()>0.98).sum()/60; ys=(w.yawIn.abs()>0.98).sum()/60
        out+=f' | psi {a:+.1f}/{b:+.1f}, r {w.r.abs().max():4.1f}, posts sat {sat:.2f}s, yaw sat {ys:.2f}s'
    print(out)
