# H-case metrics for any result dirs. usage: hm.py <dir> [<dir> ...]
import sys, pandas as pd, numpy as np
def nrev(x, thr):
    x=np.asarray(x,float); e=x[0]; d=0; n=0
    for v in x[1:]:
        if d>=0 and v<e-thr: n+=(d>0); d=-1; e=v
        elif d<=0 and v>e+thr: n+=(d<0); d=1; e=v
        elif (d>=0 and v>e) or (d<=0 and v<e): e=v
    return n
for f in sys.argv[1:]:
    d=pd.read_csv(f+'/H.csv')
    def seg(t0,t1,trel,col='vSide'):
        w=d[(d.t>=t0)&(d.t<t1)]; v=w[col].values; xs=np.cumsum(v)/60; aft=w[w.t>trel]
        sg=np.sign(v[np.argmax(np.abs(v))]); rev=max(0,-(sg*aft[col]).min()); back=np.abs(xs).max()-abs(xs[-1]); ts=aft[aft[col].abs()<0.3].t.min()-trel
        bad=aft[aft[col].abs()>0.15]; tset=(bad.t.max()-trel) if len(bad) else 0
        return np.abs(v).max()*1.944,ts,rev,back,tset
    a=seg(20,40,25.1); b=seg(40,50,41.6); u=seg(65,90,78.1,'uFwd')
    w=d[(d.t>=20)&(d.t<40)]; h0=d[(d.t>18.5)&(d.t<20)].alt.mean(); br=np.abs(np.gradient(w.phi.values,1/60)).max()
    ff=d[(d.t>=65)&(d.t<90)]; h2=d[(d.t>63)&(d.t<65)].alt.mean()
    c=d[(d.t>=90)&(d.t<110)]; h3=d[(d.t>88.5)&(d.t<90)].alt.mean(); aft=c[c.t>95.1]; spa=np.hypot(aft.vSide,aft.uFwd)
    g=d[(d.t>=109.9)&(d.t<125)]; x0,z0=g.pos_x.iloc[0],g.pos_z.iloc[0]; dist=np.hypot(g.pos_x-x0,g.pos_z-z0); h4=d[(d.t>108)&(d.t<109.9)].alt.mean()
    p=d[(d.t>=50)&(d.t<64)]; hp=d[(d.t>48)&(d.t<50)].alt.mean()
    hv=d[(d.t>17)&(d.t<19.5)]
    print(f"== {f}")
    print(f"  side full: {a[0]:.1f} kt, bank max {w.phi.abs().max():.1f}, bank rate {br:.1f}, stop<0.3 {a[1]:.2f}s, past zero {a[2]:.2f} m/s, back {a[3]:.2f} m, settled<0.15 {a[4]:.2f}s, height {w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f}, vs {w.vs.min():+.2f}/{w.vs.max():+.2f}, p reversals>1: {nrev(w.p.values,1.0)}, phiCmd rev>0.5: {nrev(w.phiCmd.values,0.5)}")
    print(f"  tap: past zero {b[2]:.2f}, back {b[3]:.2f} m, settled {b[4]:.2f}s")
    print(f"  fwd: {u[0]:.1f} kt, stop<0.3 {u[1]:.2f}s, back-run {u[2]:.2f}, back {u[3]:.2f} m, settled {u[4]:.2f}s, height {ff.alt.min()-h2:+.2f}/{ff.alt.max()-h2:+.2f}, vs {ff.vs.min():+.2f}/{ff.vs.max():+.2f}, q max {ff.q.abs().max():.1f}, q rev>1: {nrev(ff.q.values,1.0)}, thetaAssist rev>0.5: {nrev(ff.thetaAssist.values,0.5)}, thr rev>0.1: {nrev(ff.thrEng.values,0.1)}")
    print(f"  both: {np.hypot(c.vSide,c.uFwd).max()*1.944:.1f} kt, stop {aft[spa<0.3].t.min()-95.1:.2f}s, height {c.alt.min()-h3:+.2f}/{c.alt.max()-h3:+.2f}, vs {c.vs.min():+.2f}/{c.vs.max():+.2f}, thetaAssist rev>0.5: {nrev(c.thetaAssist.values,0.5)}")
    print(f"  gust: drift {dist.max():.1f} m, ends {dist.iloc[-60:].mean():.2f} m off, height {g.alt.min()-h4:+.2f}/{g.alt.max()-h4:+.2f} | pedal: peak {p.r.abs().max():.1f}, height {p.alt.min()-hp:+.2f}/{p.alt.max()-hp:+.2f}")
    ex=[c0 for c0 in ('tiltPitchA','phiFF') if c0 in d.columns]
    print(f"  hover: spare {hv.aUpBody.mean():.2f}, vmax {hv.trcVmax.mean()*1.944:.1f} kt, tilt rate side {hv.tiltRate.mean():.1f} / fwd {hv.tiltRateU.mean():.1f}"+(f", pitch tilt authority {hv.tiltPitchA.mean():.1f} deg/s2" if 'tiltPitchA' in d.columns else ''))
