# Vortex 0.5.0 vane-set metrics. usage: vnm.py <tag> [<tag> ...]   (/tmp/vn/<tag>/*.csv)
import pandas as pd, numpy as np, sys
def nrev(x, thr):
    x=np.asarray(x,float); e=x[0]; d=0; n=0
    for v in x[1:]:
        if d>=0 and v<e-thr: n+=(d>0); d=-1; e=v
        elif d<=0 and v>e+thr: n+=(d<0); d=1; e=v
        elif (d>=0 and v>e) or (d<=0 and v<e): e=v
    return n
def rd(tag,n):
    d=pd.read_csv(f'/tmp/vn/{tag}/{n}.csv')
    if 'vaneNow' not in d: d['vaneNow']=-10.0; d['vane']=-10.0
    d['tact']=(111900*d.ThrustScale if 'ThrustScale' in d else 0)
    return d
def acc(d,t0,t1,name):
    w=d[(d.t>=t0)&(d.t<t1)]; cmd=w.gsCmd.max(); h0=d[(d.t>t0-2)&(d.t<t0)].alt.mean(); dt=d.t.iloc[1]-d.t.iloc[0]
    t90=w[w.uFwd>=0.9*cmd].t.min()-t0; a=w[(w.uFwd>0.2*cmd)&(w.uFwd<0.7*cmd)]; ac=(a.uFwd.iloc[-1]-a.uFwd.iloc[0])/(a.t.iloc[-1]-a.t.iloc[0])
    ax=pd.Series(np.gradient(w.uFwd.values,dt)).rolling(12,center=True,min_periods=1).mean().values
    # curve shape: onset delay (time to 0.5 m/s2), hitches (accel reversals > 0.4 m/s2 before 80% of cmd)
    on=w.t.values[np.argmax(ax>0.5)]-t0; pre=ax[:np.argmax(w.uFwd.values>=0.8*cmd)] if (w.uFwd.values>=0.8*cmd).any() else ax
    return f"{name}: cmd {cmd*1.944:4.1f} kt, 90% in {t90:5.2f}s, acc 20-70% {ac:4.2f}, peak {ax.max():4.2f}, onset(0.5) {on:4.2f}s, acc rev>0.4 {nrev(pre,0.4)}, over {w.uFwd.max()-cmd:+.2f}, height {w.alt.min()-h0:+.3f}/{w.alt.max()-h0:+.3f}, vs {w.vs.min():+.2f}/{w.vs.max():+.2f}, pitch {w.theta.min():.2f}..{w.theta.max():.2f}, q max {w.q.abs().max():.1f}, thrEng max {w.thrEng.max():.2f}, nozKN max {w.nozKN.max():.1f}, gov<0.02 {(w.govLam<0.02).sum()*dt:.2f}s, vane {w.vaneNow.min():.1f}..{w.vaneNow.max():.1f}, sw {w.swivel.min():.3f}..{w.swivel.max():.3f}, rev thr>0.1 {nrev(w.thrEng.values,0.1)} sw>0.03 {nrev(w.swivel.values,0.03)} vane>2 {nrev(w.vaneNow.values,2.0)} q>1 {nrev(w.q.values,1.0)}"
def stop(d,t0,t1,name):
    w=d[(d.t>=t0)&(d.t<t1)]; h0=d[(d.t>t0-2)&(d.t<t0)].alt.mean(); u0=w.uFwd.iloc[0]; dt=d.t.iloc[1]-d.t.iloc[0]; sg=np.sign(u0)
    ts=w[(sg*w.uFwd)<0.3].t.min()-t0; past=max(0,-(sg*w.uFwd).min()); bad=w[w.uFwd.abs()>0.15]; tset=bad.t.max()-t0
    ax=pd.Series(np.gradient(w.uFwd.values,dt)).rolling(12,center=True,min_periods=1).mean().values
    return f"{name}: from {u0*1.944:5.1f} kt, <0.3 m/s in {ts:5.2f}s, past zero {past:.2f} m/s, settled<0.15 {tset:5.2f}s, peak decel {np.abs(ax).max():4.2f}, height {w.alt.min()-h0:+.3f}/{w.alt.max()-h0:+.3f}, vs {w.vs.min():+.2f}/{w.vs.max():+.2f}, pitch {w.theta.min():.1f}..{w.theta.max():.1f}, q max {w.q.abs().max():.1f}, thrEng max {w.thrEng.max():.2f}, vane {w.vaneNow.min():.1f}..{w.vaneNow.max():.1f}, sw {w.swivel.min():.3f}..{w.swivel.max():.3f}, rev thetaA>0.5 {nrev(w.thetaAssist.values,0.5)} thr>0.1 {nrev(w.thrEng.values,0.1)} q>1 {nrev(w.q.values,1.0)} vane>2 {nrev(w.vaneNow.values,2.0)}"
for tag in sys.argv[1:]:
    print("=====",tag)
    d=rd(tag,'P30'); hv=d[(d.t>17)&(d.t<19.5)]
    print(f"  hover: swivel {hv.swivel.mean():.3f}, vane {hv.vaneNow.mean():.2f}, thrust asked/tMx(cap) {(hv.thrustReq*1000/hv.tMx).mean():.3f}, nozKN {hv.nozKN.mean():.1f}, fanKN {hv.fanKN.mean():.1f}, spare lift {hv.aUpBody.mean():.2f}, aHlevel {hv.aHlevel.mean():.2f}, vmax {hv.trcVmax.mean()*1.944:.1f} kt, pitch {hv.theta.mean():.2f}")
    print("  "+acc(d,20.1,32,'P30 accel')); print("  "+stop(d,32.1,45,'P30 stop'))
    d=rd(tag,'P45'); print("  "+acc(d,20.1,35,'P45 accel')); w=d[(d.t>=20.1)]; print(f"      time to 39 kt {w[w.uFwd>=39/1.944].t.min()-20.1:.2f}s"); print("  "+stop(d,35.1,50,'P45 stop'))
    d=rd(tag,'P2'); w=d[(d.t>23.2)&(d.t<30)]; t5=w[w.phi.abs()>5].t.min()-23.2; h0=d[(d.t>18)&(d.t<20)].alt.mean(); ww=d[(d.t>=20.1)&(d.t<30)]
    fr=((w.phiCmd.abs()<1.0)&(w.t<26)).sum()/60
    print(f"  P2 side stick 3 s into 0->45: bank>5 deg {t5:.2f}s after stick, phiCmd<1 deg for {fr:.2f}s, vSide at +2.9s {d[d.t>=26.1].iloc[0].vSide:.2f} m/s, uFwd at 30s {d[d.t>=30].iloc[0].uFwd:.1f}, height {ww.alt.min()-h0:+.3f}/{ww.alt.max()-h0:+.3f}, vs {ww.vs.min():+.2f}/{ww.vs.max():+.2f}, gov<0.02 {(ww.govLam<0.02).sum()/60:.2f}s, phiCmd rev>0.5 {nrev(w.phiCmd.values,0.5)}, p rev>1 {nrev(w.p.values,1.0)}, post sat {(ww.postIn.abs()>0.98).sum()/60:.2f}s, thr max {ww.thrEng.max():.2f}")
    d=rd(tag,'PS'); w=d[(d.t>30.7)&(d.t<34.1)]; t2=w[w.phi.abs()>2].t.min()-30.7; t5=w[w.phi.abs()>5].t.min()-30.7; h0=d[(d.t>28)&(d.t<30)].alt.mean(); ww=d[(d.t>=30.1)&(d.t<45)]
    print(f"  PS side stick during the stop: bank>2/5 deg {t2:.2f}/{t5:.2f}s, phiCmd<1 deg {((w.phiCmd.abs()<1.0)).sum()/60:.2f}s, height {ww.alt.min()-h0:+.3f}/{ww.alt.max()-h0:+.3f}, vs {ww.vs.min():+.2f}/{ww.vs.max():+.2f}, pitch max {ww.theta.max():.1f}")
    d=rd(tag,'PB'); c=d[(d.t>=20.1)&(d.t<45)]; h0=d[(d.t>18)&(d.t<20)].alt.mean(); aft=c[c.t>28.1]; spa=np.hypot(aft.vSide,aft.uFwd); w8=c[c.t<28.1]
    print(f"  PB side + fore-aft 8 s: speed {np.hypot(w8.vSide,w8.uFwd).max()*1.944:.1f} kt (u {w8.uFwd.max()*1.944:.1f}, v {w8.vSide.abs().max()*1.944:.1f}), stop<0.3 {aft[spa<0.3].t.min()-28.1:.2f}s, height {c.alt.min()-h0:+.3f}/{c.alt.max()-h0:+.3f}, vs {c.vs.min():+.2f}/{c.vs.max():+.2f}, pitch {c.theta.min():.1f}..{c.theta.max():.1f}, bank max {c.phi.abs().max():.1f}, thetaA rev {nrev(c.thetaAssist.values,0.5)}, phiCmd rev {nrev(c.phiCmd.values,0.5)}, thr rev {nrev(c.thrEng.values,0.1)}, gov<0.02 {(c.govLam<0.02).sum()/60:.2f}s")
    d=rd(tag,'PBK'); print("  "+stop(d,30.1,45,'PBK stop from backwards')); w=d[(d.t>=20.1)&(d.t<30)]; h0=d[(d.t>18)&(d.t<20)].alt.mean(); print(f"      backwards accel: u min {w.uFwd.min()*1.944:.1f} kt, 90% in {w[w.uFwd<=0.9*w.gsCmd.min()].t.min()-20.1:.2f}s, height {w.alt.min()-h0:+.3f}/{w.alt.max()-h0:+.3f}, pitch {w.theta.min():.1f}..{w.theta.max():.1f}, vane {w.vaneNow.min():.1f}..{w.vaneNow.max():.1f}")
    for n,ti,to in (('Y30',30.1,38.1),('Y45',35.1,43.1)):
        d=rd(tag,n); w=d[(d.t>ti)&(d.t<to)]; x=d[(d.t>to)&(d.t<to+10)]
        print(f"  {n} pedal 8 s: skid max {w.vSide.abs().max():.2f} m/s (beta {w.beta.abs().max():.1f}), u {w.uFwd.min():.2f}..{w.uFwd.max():.2f}, alt {w.alt.min():.2f}..{w.alt.max():.2f}, yaw rate max {w.psiRate.abs().max():.1f} end {w.psiRate.iloc[-1]:.1f} | exit skid {x.vSide.min():.2f}..{x.vSide.max():.2f}, bank max {x.phi.abs().max():.1f}, alt {x.alt.min():.2f}..{x.alt.max():.2f} | post sat {(d[d.t>ti].postIn.abs()>0.98).sum()/60:.2f}s, vane {w.vaneNow.min():.1f}..{w.vaneNow.max():.1f}")
