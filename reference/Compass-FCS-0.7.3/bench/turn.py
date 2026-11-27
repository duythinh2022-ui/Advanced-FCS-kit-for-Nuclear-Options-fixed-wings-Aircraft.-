import sys,pandas as pd,numpy as np
for t in sys.argv[1:]:
    d=pd.read_csv(f'runs/{t}.csv'); dt=d.t.diff().median()
    w=d[d.sr.abs()>0.5]; w=w[w.t>w.t.iloc[0]+1.5]
    n=int(1/dt)
    hp=lambda x:np.sqrt(np.nanmean(((x-x.rolling(n,center=True).mean()))**2))
    print(f"{t:18s} ias {w.ias.iloc[0]:4.0f}->{w.ias.iloc[-1]:4.0f} | r mean {w.r.abs().mean():5.1f} range {w.r.abs().quantile(.05):5.1f}-{w.r.abs().quantile(.95):5.1f} HP {hp(w.r):5.2f} | pSmax HP {hp(w.pSmax):5.2f} pMax {w.pMax.mean():5.1f} HP {hp(w.pMax):5.2f} rMax {w.rMax.mean():5.1f} | alpha min {w.alpha.min():5.1f} max {w.alpha.max():5.1f} HP {hp(w.alpha):4.2f} prot frac {(w.prot>0).mean()*100:4.1f}% | beta max {w.beta.abs().max():4.1f} | stab HP {hp(w.stabL):4.1f}")
