import sys,pandas as pd,numpy as np
for t in sys.argv[1:]:
    d=pd.read_csv(f'runs/{t}.csv'); dt=d.t.diff().median()
    w=d[d.yawIn.abs()>0.5]; w=w[w.t>w.t.iloc[0]+1.0]
    n=int(0.4/dt)
    hp=lambda x:np.sqrt(np.nanmean((x-x.rolling(n,center=True).mean())**2))
    wig=lambda c:np.nanmean(np.abs(np.diff(w[c].values)))/dt
    print(f"{t:24s} ias {w.ias.iloc[0]:4.0f}->{w.ias.min():4.0f} a {w.alpha.mean():4.1f} | |r| mean {w.r.abs().mean():5.1f} HP {hp(w.r):5.2f} | rCmd HP {hp(w.rCmd):5.2f} rMax mean {w.rMax.mean():5.1f} HP {hp(w.rMax):5.2f} | surf |rate| stab {wig('stabL'):5.1f}/{wig('stabR'):5.1f} flap {wig('flapL'):5.1f} ail {wig('ailL'):5.1f} rud {wig('rudL'):5.1f} | beta {w.beta.abs().max():4.1f} | qK min {w.qPredK.min():4.2f} idot min {w.iasDotF.min():5.1f}")
