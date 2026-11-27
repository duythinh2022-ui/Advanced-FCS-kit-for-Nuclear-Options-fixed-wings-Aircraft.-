import sys, pandas as pd, numpy as np
def nrev(x, thr):
    x=np.asarray(x,float); e=x[0]; d=0; n=0
    for v in x[1:]:
        if d>=0 and v<e-thr: n+=(d>0); d=-1; e=v
        elif d<=0 and v>e+thr: n+=(d<0); d=1; e=v
        elif (d>=0 and v>e) or (d<=0 and v<e): e=v
    return n
for tag in sys.argv[1:]:
    try:
        d=pd.read_csv(f'/tmp/b3/{tag}/D.csv'); on=d[d.state==2]
        if not (on.gs<1).any(): print(f"{tag:14s} no hover reached (last gs {on.gs.iloc[-1]:.1f} kt)"); continue
        te=on[on.gs<1].t.iloc[0]; hf=on[(on.t>te+5)&(on.t<te+8)].alt.mean(); ww=on[(on.t>te-8)&(on.t<te+5)]
        print(f"{tag:14s} decel end: alt vs final {ww.alt.min()-hf:+.2f}/{ww.alt.max()-hf:+.2f}, vs {ww.vs.min():+.2f}/{ww.vs.max():+.2f}, |vs|>0.2 for {(ww.vs.abs()>0.2).sum()/60:.2f} s, pitch max {ww.theta.max():.1f}, q max {ww.q.abs().max():.1f}, throttle reversals>0.1: {nrev(ww.thrEng.values,0.1)}, tilt rate {ww.tiltRateU.max():.1f}")
    except Exception as e: print(tag,'ERR',e)
