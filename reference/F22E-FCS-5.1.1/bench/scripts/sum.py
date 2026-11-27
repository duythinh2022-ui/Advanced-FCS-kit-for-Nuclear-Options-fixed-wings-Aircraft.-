import pandas as pd, sys, numpy as np
f=sys.argv[1]; t0=float(sys.argv[2]) if len(sys.argv)>2 else 2.0
d=pd.read_csv(f)
w=d[d.t>=t0]
pre=d[(d.t>t0-0.2)&(d.t<t0)]
i=w.beta.abs().idxmax()
print(f"alpha@roll {pre.alpha.mean():.1f} nz {pre.nz.mean():.1f} ias {pre.ias.mean():.0f} | pMax {w.p.abs().max():.0f} rMax {w.r.abs().max():.0f} omegaMax {w.omegaV.abs().max():.0f} | betaMax {d.loc[i,'beta']:.1f} at {d.loc[i,'t']:.2f} | rudL range {w.rudL.min():.0f}..{w.rudL.max():.0f}")
if len(sys.argv)>3:
    print(d[(d.t>t0-0.1)&(d.t<t0+2.5)][["t","alpha","beta","nz","p","r","sr","pCmd","rCmd","pSmax","rudL","ailL","ailR","flapL","flapR","stabL","stabR","aYaw","latAcc"]].iloc[::5].to_string(index=False,float_format=lambda x:f"{x:.1f}"))
