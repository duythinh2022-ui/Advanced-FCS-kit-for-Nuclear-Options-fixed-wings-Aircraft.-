# pitch-rate curve quality while the stick is held toward the limit: re-rises of q after it starts falling (hitches), the worst
# 0.1 s change in pitch acceleration (jerk spikes), and pitch-rate chatter (sign changes of qdot)
import pandas as pd, numpy as np, json, sys
W=json.load(open("windows.json")); tag, ref, cg = sys.argv[1:4]
def m(d):
    d=d[d.sp>0.5]; 
    if len(d)<10: return None
    q=pd.Series(d.q_b.values).rolling(3,center=True,min_periods=1).mean().values; t=d.t.values
    qd=np.gradient(q,t); ip=int(np.argmax(d.alpha_b.values))
    seg=q[:ip+1]; run=np.maximum.accumulate(seg[::-1])[::-1]   # q should fall monotonically after its max
    imax=int(np.argmax(seg)); after=seg[imax:]; rer=0.0
    lo=after[0]
    for x in after: lo=min(lo,x); rer=max(rer,x-lo)
    jerk=np.abs(np.diff(qd[::6])).max() if len(qd)>12 else 0
    chat=int(np.sum(np.diff(np.sign(qd[imax:ip+1]))!=0)) if ip>imax+2 else 0
    return rer, jerk, chat
R=[]
for w in W:
    a=m(pd.read_csv(f"out/{ref}_{cg}/w{w['id']:02d}.csv")); b=m(pd.read_csv(f"out/{tag}_{cg}/w{w['id']:02d}.csv"))
    if a and b: R.append(dict(id=w['id'],reR=a[0],reN=b[0],jkR=a[1],jkN=b[1],chR=a[2],chN=b[2]))
R=pd.DataFrame(R)
print(f"{tag} vs {ref} CG {cg}: q re-rise after its max (hitch) median {R.reR.median():.2f} -> {R.reN.median():.2f}, max {R.reR.max():.1f} -> {R.reN.max():.1f}, windows with a re-rise > 2 deg/s: {(R.reR>2).sum()} -> {(R.reN>2).sum()}")
print(f"   worst 0.1 s change in pitch accel median {R.jkR.median():.0f} -> {R.jkN.median():.0f}, max {R.jkR.max():.0f} -> {R.jkN.max():.0f};  qdot sign changes in the slowdown median {R.chR.median():.0f} -> {R.chN.median():.0f}")
print(R[(R.reN>2)&(R.reN>R.reR+1)].round(1).to_string())
