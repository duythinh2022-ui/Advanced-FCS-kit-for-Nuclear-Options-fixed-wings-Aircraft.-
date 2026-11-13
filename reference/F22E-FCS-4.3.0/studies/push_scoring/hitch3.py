import pandas as pd, numpy as np
def rev(q,mask):
    """largest backing-off of the nose-down pitch rate (deg/s) inside mask: q rises above its running minimum"""
    best=0.0; mn=1e9
    for v,m in zip(q,mask):
        mn=min(mn,v)
        if m: best=max(best,v-mn)
    return best
def pushes(path):
    d=pd.read_csv(path,low_memory=False); d=d[d['mode']==3].reset_index(drop=True); out=[]; last=-9
    for i in range(5,len(d)):
        if d.alpha[i]>40 and d.sp[i]<-0.3 and d.sp[i-3]>-0.3 and d.t[i]-last>3:
            last=d.t[i]; t0=d.t[i]; w=d[(d.t>=t0)&(d.t<=t0+2.2)&(d.sp<-0.3)]
            q=w.q.values; a=w.alpha.values; tv=w.tvcNow.values
            early=rev(q,a>30); late=rev(q,(a<=30)&(a>8))
            # nozzle reversal: after reaching <= -8 nose-down, swings back to >= +3 before AoA < 20
            m=a>20; noz=0.0
            if m.any():
                tvm=tv[m]; mn=np.minimum.accumulate(tvm); noz=float(np.max(np.where(mn<=-8,tvm-mn,0)))
            out.append(dict(t=round(t0,1),early=round(early,1),late=round(late,1),nozzle_back=round(noz,1),r0=round(d.r[i])))
    return out
