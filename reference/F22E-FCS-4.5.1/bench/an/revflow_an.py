import pandas as pd, sys
cases={'A':('stick R',18,19.5,'sr'),'B':('stick L',21,22.5,'sr'),'C':('pedal L',18,19.5,'yawIn'),'D':('stick R (neg AoA)',17,18.5,'sr'),'E':('stick R thru 90',15.5,17,'sr'),'F':('pedal R',21,22.5,'yawIn')}
for k,(nm,t0,t1,col) in cases.items():
    print(f"=== {k}: {nm}, input {t0}-{t1} s")
    for tag in sys.argv[1:]:
        d=pd.read_csv(f'/tmp/rv_{tag}_{k}.csv')
        w=d[(d.t>=t0)&(d.t<=t1+1.5)]
        s=w[(w.t<=t1)]
        sg=1 if s[col].mean()>0 else -1
        # body rates at end of input
        e=s.iloc[-1]
        print(f" {tag:4s} AoA {s.alpha.iloc[0]:6.1f}->{e.alpha:6.1f}  ias {e.ias:4.0f} | at input end: p {e.p:6.1f} r {e.r:6.1f} (input sign {sg:+d}) pS {e.pS:6.1f} pCmd {e.pCmd:6.1f} rCmd {e.rCmd:6.1f} beta {e.beta:6.1f} rS {e.rS:5.1f} | peak|beta| {w.beta.abs().max():5.1f}  phi chg {e.phi-s.phi.iloc[0]:6.1f}")
