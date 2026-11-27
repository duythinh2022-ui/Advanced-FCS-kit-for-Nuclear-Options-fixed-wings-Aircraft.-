import sys,pandas as pd,numpy as np
for t in sys.argv[1:]:
    d=pd.read_csv(f'runs/{t}.csv'); dt=d.t.diff().median()
    lat=d[(d.sr.abs()>0.5)|(d.sp.abs()>2)] if 'sp' in d else d
    w=d[d.sr.abs()>0.5]
    if len(w)==0: print(t,'no roll'); continue
    t1=w.t.iloc[-1]; t0=w.t.iloc[0]
    rel=d[d.t>t1]
    om=np.hypot(rel.p,rel.r)
    stopi=np.argmax(om.values<5) if (om.values<5).any() else -1
    tstop=(rel.t.iloc[stopi]-t1) if stopi>=0 else float('nan')
    # angle traveled (velocity-vector roll) after release until stop
    ang=np.sum(np.abs(rel.omegaV.values[:stopi if stopi>0 else len(rel)]))*dt
    ww=d[(d.t>=t0)&(d.t<=t1)]
    print(f"{t:20s} roll {t0:5.2f}..{t1:5.2f} ias {ww.ias.iloc[0]:4.0f}->{ww.ias.iloc[-1]:4.0f} | |p|max {ww.p.abs().max():5.1f} |r|max {ww.r.abs().max():5.1f} omV max {ww.omegaV.abs().max():5.1f} at-release omV {abs(ww.omegaV.iloc[-1]):5.1f} | pMax end {ww.pMax.iloc[-1]:5.1f} rMax end {ww.rMax.iloc[-1]:5.1f} aRoll {ww.aRoll.iloc[-1]:5.1f} aYaw {ww.aYaw.iloc[-1]:5.1f} | stop {tstop:4.2f}s over {ang:5.1f}deg | beta max {d[d.t>=t0].beta.abs().max():4.1f} | qK min {ww.qPredK.min():4.2f}")
