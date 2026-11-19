import sys, pandas as pd, numpy as np
def lp(x,tau,dt):
    y=np.empty_like(x); y[0]=x[0]; a=dt/(tau+dt) if tau>0 else 1.0
    for i in range(1,len(x)): y[i]=y[i-1]+a*(x[i]-y[i-1])
    return y
def fit(d,cmd,out,t0,t1):
    w=d[(d.t>=t0)&(d.t<t1)]; c=w[cmd].values.astype(float); r=w[out].values.astype(float); dt=float(np.median(np.diff(w.t.values)))
    return min(((np.sqrt(np.mean((lp(c,tau,dt)-r)**2)),tau) for tau in np.arange(0,1.5,0.01)))
TT=float(sys.argv[1]) if len(sys.argv)>1 else 1.83; TL=float(sys.argv[2]) if len(sys.argv)>2 else 0.436
for tag in sys.argv[3:]:
    H=pd.read_csv(f'/tmp/p0/{tag}/H.csv')
    w=H[(H.t>17)&(H.t<19.5)]
    aH=w.aHlevel.mean(); aR=w.authP.mean()*w.kRollS.mean(); aP=w.authQ.mean(); aY=w.authR.mean(); tv=w.tauV.mean(); aUp=w.aUpBody.mean()
    e1=fit(H,'phiCmd','phi',20,32); e2=fit(H,'thetaRef','theta',65,95)
    r1=fit(H,'pCmd','p',20,30)[1]; r2=fit(H,'rCmd','r',50,58)[1]; r3=fit(H,'qCmd','q',65,90)[1]
    s=H[(H.t>=20)&(H.t<40)]; dt=np.median(np.diff(s.t)); br=np.abs(np.gradient(s.phi.values,dt)).max(); vm=s.vSide.abs().max()*1.944
    aft=s[s.t>25.1]; ts=aft[(aft.vSide.abs()<0.3)].t.min()-25.1; h0=H[(H.t>18.5)&(H.t<20)].alt.mean()
    f=H[(H.t>=65)&(H.t<100)]; af=f[f.t>78.1]; tf=af[(af.uFwd.abs()<0.3)].t.min()-78.1
    y=H[(H.t>=50)&(H.t<58)]
    print(f"{tag:7s} mass {w.mass.mean():.0f}: aHlevel {aH:.2f} aUpBody {aUp:.2f} tauV {tv:.3f} | aRoll*kS {aR:.1f} aPitch {aP:.1f} aYaw {aY:.1f} | rate lags p {r1:.2f} q {r3:.2f} r {r2:.2f} | angle-loop lag: bank {e1[1]:.2f} s, pitch {e2[1]:.2f} s")
    print(f"        today: side bank rate {br:.1f} deg/s, speed {vm:.1f} kt, bank {s.phi.abs().max():.1f}, stop {ts:.2f} s, height {s.alt.min()-h0:+.2f}/{s.alt.max()-h0:+.2f} | fore-aft stop {tf:.2f} s, pitch max {f.theta.max():.1f}, pitch rate {f.q.abs().max():.1f}, height {f.alt.min()-H[(H.t>63)&(H.t<65)].alt.mean():+.2f} | yaw peak {y.r.abs().max():.1f}")
    tl=np.arctan2(aH,9.81); tt=0.2*aUp/(9.81*np.tan(tl)/np.cos(tl)*tv)*57.3 if aH>0.05 else float('nan')
    print(f"        derived (T_trc {TT} s, T_tilt {TL} s): v_max {aH*TT*1.944:.1f} kt, tilt rate roll {aR*TL:.1f} deg/s, pitch {aP*TL:.1f} deg/s; thrust term at full tilt ({np.degrees(tl):.0f} deg): {tt:.1f} deg/s")
