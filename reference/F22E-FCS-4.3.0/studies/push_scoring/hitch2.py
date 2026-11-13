import numpy as np
def dip(t,qc,a,sp,t0,t1=None):
    """depth of a two-sided dip in the nose-down command (deg/s): it eases and then increases again,
    while the stick is pushed and |AoA| > 12 after the push mark"""
    t=np.asarray(t); a=np.asarray(a); qc=np.asarray(qc); sp=np.asarray(sp)
    m=(t>=t0)&(np.abs(a)>12)&(np.sign(a)*sp< -0.3)
    if t1 is not None: m&=(t<=t1)
    if m.sum()<5: return 0.0
    s=np.sign(a[m][0]); x=-s*qc[m]            # nose-down magnitude
    pre=np.maximum.accumulate(x); post=np.maximum.accumulate(x[::-1])[::-1]
    return float(np.max(np.minimum(pre-x,post-x)))
