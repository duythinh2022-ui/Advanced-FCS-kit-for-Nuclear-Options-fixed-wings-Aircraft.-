import pandas as pd, numpy as np, sys
def acc(f,t0,t1):
    d=pd.read_csv(f); a=d[(d.t>=t0)].iloc[0]; b=d[(d.t>=t1)].iloc[0]
    w=d[(d.t>t0)&(d.t<t1)]
    return (b.uFwd-a.uFwd)/(b.t-a.t), (w.thrustReq/(w.tMx*1e-3)).mean(), w.alt.max()-w.alt.min(), w.govLam.mean()
for n in sys.argv[1].split(','):
    print(n, "fwd accel 25-29 s %.2f m/s2, thrust ratio %.2f, alt band %.2f m, govLam mean %.2f"%acc('/tmp/'+n+'.csv',25,29))
for n in sys.argv[2].split(','):
    d=pd.read_csv('/tmp/'+n+'.csv'); w=d[(d.t>23.1)&(d.t<30)]
    t5=w[w.phi>5].t.min(); 
    print(n, "P2: fwd accel 21-23 %.2f; bank>5deg at t=%.2f (stick 23.1); vSide at 25/26/28/30 s: %.1f %.1f %.1f %.1f; uFwd at 30: %.1f; alt band %.2f; vmax %.1f"%(acc('/tmp/'+n+'.csv',21,23)[0], t5, d[d.t>=25].iloc[0].vSide, d[d.t>=26].iloc[0].vSide, d[d.t>=28].iloc[0].vSide, d[d.t>=30].iloc[0].vSide, d[d.t>=30].iloc[0].uFwd, w.alt.max()-w.alt.min(), d[d.t>=26].iloc[0].trcVmax))
