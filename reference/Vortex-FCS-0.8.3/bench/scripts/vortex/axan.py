import pandas as pd,numpy as np,sys,warnings; warnings.filterwarnings('ignore')
def L(n,tag): return pd.read_csv(f'/tmp/ax/{n}_{tag}.csv')
def first(d,t0,c):
    w=d[(d.t>=t0)&c]; return (w.t.iloc[0]-t0) if len(w) else float('nan')
for tag in sys.argv[1:]:
    print('==',tag)
    for n,tc in [('C0',20.5),('CS5',20.5),('CS0',21),('SC5',21.5),('CSh',20.5),('CF5',20.5)]:
        d=L(n,tag); a0=d[d.t>=tc].alt.iloc[0]; w=d[(d.t>=tc)&(d.t<=27)]; e=d[(d.t>=27)&(d.t<=33)]
        ts=first(d,27.1,d.vs.abs()<0.1)
        print(f"  {n:4s} climb: to 2.5 m/s {first(d,tc,d.vs>=2.5):.2f}s, +3 s height {d[d.t>=tc+3].alt.iloc[0]-a0:.2f} m, lowest vs {w.vs.min():+.2f} | stop: |vs|<0.1 after {ts:.2f}s, undershoot {e.vs.min():+.2f}")
    for n,amp,toff in [('S0',1,27),('CS5',1,27),('CS0',1,27),('SC5',1,27),('Sh',0.4,27),('CSh',0.4,27),('SCS',1,29),('SD',1,29)]:
        d=L(n,tag); w=d[(d.t>=21)&(d.t<=toff)]; e=d[(d.t>=toff)&(d.t<=toff+8)]
        vp=d.vPiv; pk=w.vPiv.abs().max(); h0=d[(d.t>20)&(d.t<20.4)].psi.mean()
        past=(-np.sign(w.vPiv.iloc[-1])*e.vPiv).max()
        tstop=first(d,toff+0.1,d.vPiv.abs()<0.3)
        hk=((d[(d.t>=20.5)&(d.t<=toff+8)].psi-h0+180)%360-180).abs().max(); rk=d[(d.t>=20.5)&(d.t<=toff+8)].r.abs().max()
        print(f"  {n:4s} side {amp}: bank +1 s {abs(d[d.t>=22].phi.iloc[0]):4.1f}, to 1 m/s {first(d,21,d.vPiv.abs()>=1):.2f}s, to 3 {first(d,21,d.vPiv.abs()>=3):.2f}s, top {pk:.2f} (cmd {d[(d.t>25)&(d.t<26)].trcVmax.mean()*amp:.2f}) | stop to 0.3 in {tstop:.2f}s, past zero {max(0,past):.2f} | bank max {d[(d.t>=21)&(d.t<=toff+8)].phi.abs().max():.1f} | hdg off {hk:.1f} deg, yaw rate {rk:.1f} | vs {d[(d.t>=21)&(d.t<=toff+8)].vs.min():+.2f}/{d[(d.t>=21)&(d.t<=toff+8)].vs.max():+.2f} govLam min {w.govLam.min():.2f}")
    for n in ['F0','CF5']:
        d=L(n,tag); w=d[(d.t>=21)&(d.t<=27)]; e=d[(d.t>=27)&(d.t<=36)]
        print(f"  {n:4s} lever: to 2 m/s {first(d,21,d.uFwd>=2):.2f}s, to 8 {first(d,21,d.uFwd>=8):.2f}s, pitch {d[(d.t>=21)&(d.t<=36)].theta.min():+.1f}/{d[(d.t>=21)&(d.t<=36)].theta.max():+.1f} | stop to 0.3 in {first(d,27.1,d.uFwd.abs()<0.3):.2f}s, past zero {max(0,-e.uFwd.min()):.2f} | height during stop {e.alt.min()-d[d.t>=27].alt.iloc[0]:+.2f}/{e.alt.max()-d[d.t>=27].alt.iloc[0]:+.2f}")
    d=L('CY5',tag); print(f"  CY5  hover pedal after climb: half rate {first(d,21,d.psiRate.abs()>=12):.2f}s, peak {d[(d.t>21)&(d.t<25)].psiRate.abs().max():.1f}, pivot skid {d[(d.t>21)&(d.t<30)].vPiv.abs().max():.2f}")
