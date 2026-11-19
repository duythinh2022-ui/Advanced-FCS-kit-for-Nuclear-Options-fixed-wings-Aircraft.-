#!/usr/bin/env python3
# Online per-group gain identification (RLS on 0.1 s differences, forgetting tau), replayed over each log.
import numpy as np, pandas as pd, sys
S='/tmp/claude-0/-home-claude/c3114abd-20fc-5984-be11-c25d56f03f2d/scratchpad'
D=pd.read_pickle(f'{S}/D.pkl')
tau=float(sys.argv[1]) if len(sys.argv)>1 else 3.0
k=6; dt=1/60.; lam=np.exp(-dt/tau)
def run(g, axis):
    if axis=='z': cols=['rudZnow','stabZnow','flapZnow']; y=g.rdot.ewm(halflife=3).mean().values
    else: cols=['rudXnow','stabXnow','flapXnow']; y=g.pdot.ewm(halflife=3).mean().values
    X=g[cols].values
    n=3; th=np.ones(n); P=np.eye(n)*1.0; prior=np.ones(n); ridge=0.002
    out=np.zeros((len(g),n));
    for i in range(len(g)):
        if i>=k:
            x=X[i]-X[i-k]; yy=y[i]-y[i-k]
            # excitation gate: the group must have moved
            if np.abs(x).max()>3.0:
                # ridge toward prior: treat as an extra observation each tick
                Px=P@x; den=lam+x@Px
                K=Px/den; th=th+K*(yy-x@th); P=(P-np.outer(K,Px))/lam
                th=th+ridge*(prior-th)
            P=np.minimum(P,5.0)
        out[i]=np.clip(th,0.2,1.6)
    return out
pd.set_option('display.width',200)
for tag,g in D.groupby('tag'):
    g=g.reset_index(drop=True)
    gz=run(g,'z'); gx=run(g,'x')
    g['gZr'],g['gZs'],g['gZf']=gz.T; g['gXr'],g['gXs'],g['gXf']=gx.T
    # summary by AoA band (medians of the running estimate)
    print(f'== {tag[-6:]} tau {tau}')
    for lo,hi in [(0,20),(20,40),(40,60),(60,90)]:
        s=g[(g.alpha>=lo)&(g.alpha<hi)]
        if len(s)<100: continue
        q=lambda c:f'{s[c].median():.2f}[{s[c].quantile(.1):.2f}-{s[c].quantile(.9):.2f}]'
        print(f'  {lo:2d}-{hi:2d} n={len(s):5d}  yaw rud {q("gZr")} stab {q("gZs")} flap {q("gZf")} | roll rud {q("gXr")} stab {q("gXs")} flap {q("gXf")}')
    g[['t','alpha','gZr','gZs','gZf','gXr','gXs','gXf']].to_pickle(f'{S}/rls_{tag}.pkl')
