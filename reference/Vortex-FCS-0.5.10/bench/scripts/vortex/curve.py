# curve quality per case: swivel target reversals, nozzle reversals, throttle chatter, rate-command hitches. usage: curve.py tag...
import sys, pandas as pd, numpy as np
W={'dec':(24.5,70),'cb':(24.5,70),'ys':(34.5,75),'t30':(39.5,70),'bk':(44.5,75),'hov':(19.5,62),'sk4':(24.5,60)}
def revs(x,amp):
    n=0; lo=hi=x[0]; d=0
    for v in x:
        if d==0:
            hi=max(hi,v); lo=min(lo,v)
            if hi-v>=amp: d=-1; lo=v
            elif v-lo>=amp: d=1; hi=v
        elif d==1:
            if v>hi: hi=v
            elif hi-v>=amp: n+=1; d=-1; lo=v
        else:
            if v<lo: lo=v
            elif v-lo>=amp: n+=1; d=1; hi=v
    return n
for tag in sys.argv[1:]:
    o=[]
    for c,(a,b) in W.items():
        try: d=pd.read_csv(f'/tmp/lg/{c}_{tag}.csv')
        except: continue
        w=d[(d.t>=a)&(d.t<=b)]; dt=d.t.diff().median()
        thr=w.thrEng.values; jit=np.abs(np.diff(thr)).sum()/(b-a)   # throttle travel per second
        o.append(f"{c}: swTgt rev {revs(w.swivelTgt.values,0.05)}, noz rev {revs(w.swivel.values,0.03)}, thr rev {revs(thr,0.1)}, thr travel {jit:.2f}/s, rpm rev {revs(w.rpm.values,40)}, vs rev {revs(w.vs.values,0.15)}, p rev {revs(w.p.values,8)}, q rev {revs(w.q.values,4)}")
    print(tag); print('  '+'\n  '.join(o))
