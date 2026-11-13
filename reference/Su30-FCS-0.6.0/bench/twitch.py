import sys,pandas as pd,numpy as np
for t in sys.argv[1:]:
    d=pd.read_csv(f'runs/{t}.csv'); dt=d.t.diff().median()
    w=d[(d.t>d.t.min()+2.0)&(d.t<-0.1)&(d.asymCap<64)]
    if len(w)<10: print(t,'no cap'); continue
    hp=w.alphaCmd-w.alphaCmd.rolling(int(1/dt),center=True).mean()
    cr=np.abs(np.diff(w.asymCap.values))/dt
    print(f"{t:22s} cap mean {w.asymCap.mean():5.1f} [{w.asymCap.min():5.1f},{w.asymCap.max():5.1f}] raw [{w.asymCapPos.clip(upper=99).min():5.1f},{w.asymCapPos.clip(upper=99).max():5.1f}] | capEff rate p90 {np.percentile(cr,90):5.1f} | alphaCmd HP rms {np.sqrt(np.nanmean(hp**2)):5.2f} | alpha HP rms {np.sqrt(np.nanmean((w.alpha-w.alpha.rolling(int(1/dt),center=True).mean())**2)):5.2f} | ias {w.ias.max():4.0f}->{w.ias.min():4.0f} qPredK min {w.qPredK.min():4.2f}")
