import pandas as pd,numpy as np,sys
d=pd.read_csv(sys.argv[1]); t0=float(sys.argv[2]) if len(sys.argv)>2 else 1.5
s=d[d.t>t0]
b=s.beta.values; tt=s.t.values
# oscillation: std of beta about 0.6s moving mean; late window (last half) too
def osc(x): return np.std(x-pd.Series(x).rolling(36,center=True,min_periods=1).mean().values)
late=s[s.t>s.t.iloc[0]+(s.t.iloc[-1]-s.t.iloc[0])/2]
fl=np.gradient((s.flapL-s.flapR).values/2,tt)
print(f"|b|max {np.abs(b).max():4.1f} bOsc {osc(b):4.2f} bOscLate {osc(late.beta.values):4.2f} pOsc {osc(s.p.values):5.2f} pOscLate {osc(late.p.values):5.2f} decelRateSat {(np.abs(fl)>90).mean():.2f} aMean {s.alpha.mean():4.1f} iasEnd {s.ias.iloc[-1]:.0f}")
