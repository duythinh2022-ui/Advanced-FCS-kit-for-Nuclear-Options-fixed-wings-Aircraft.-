import sys, pandas as pd, numpy as np
def nrev(x, thr):
    # reversals of a signal's slope larger than thr (hitches / chatter)
    x=np.asarray(x,float); ext=[x[0]]; d=0; n=0
    for v in x[1:]:
        if d>=0 and v<ext[-1]-thr: n+= (d>0); d=-1; ext.append(v)
        elif d<=0 and v>ext[-1]+thr: n+= (d<0); d=1; ext.append(v)
        elif (d>=0 and v>ext[-1]) or (d<=0 and v<ext[-1]): ext[-1]=v
    return n
def H(o):
    d=pd.read_csv(f'{o}/H.csv'); dt=np.median(np.diff(d.t)); r={}
    h0=d[(d.t>18.5)&(d.t<20)].alt.mean(); w=d[(d.t>=20)&(d.t<40)]
    v=w.vSide.values; vm=np.abs(v[w.t.values<25.1]).max(); t90=w[(w.vSide.abs()>=0.9*vm)].t.iloc[0]-20.1
    aft=w[w.t>25.1]; ts=aft[aft.vSide.abs()<0.3].t.min()-25.1; ov=max(0,-(np.sign(v[np.argmax(np.abs(v))])*aft.vSide).min())
    ph=np.gradient(w.phi.values,dt); acc=np.gradient(w.vSide.values,dt)
    r['side: full-stick speed kt']=vm*1.944; r['side: t to 90 % s']=t90; r['side: bank max deg']=w.phi.abs().max(); r['side: bank rate max deg/s']=np.abs(ph).max()
    r['side: accel max m/s2']=np.abs(acc).max(); r['side: stop to 0.3 m/s s']=ts; r['side: overshoot m/s']=ov; r['side: height min m']=w.alt.min()-h0; r['side: height max m']=w.alt.max()-h0; r['side: vs min']=w.vs.min(); r['side: vs max']=w.vs.max()
    r['side: roll-rate reversals >1 deg/s']=nrev(w.p.values,1.0); r['side: bank-cmd reversals >0.5 deg']=nrev(w.phiCmd.values,0.5)
    w=d[(d.t>=40)&(d.t<50)]; r['tap: speed max kt']=w.vSide.abs().max()*1.944; r['tap: back to 0.3 m/s at s']=w[(w.t>41.6)&(w.vSide.abs()<0.3)].t.min()-41.6; r['tap: height min/max']=f"{w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f}"
    w=d[(d.t>=50)&(d.t<64)]; h1=d[(d.t>48.5)&(d.t<50)].alt.mean(); aft=w[w.t>53.1]
    psi=np.unwrap(np.radians(w.psi.values)); psi=np.degrees(psi); pf=psi[-1]; pk=psi[np.argmax(np.abs(psi-psi[0]))]
    r['pedal: yaw peak deg/s']=w.r.abs().max(); r['pedal: heading change deg']=pf-psi[0]; r['pedal: overshoot deg']=abs(pk-pf); r['pedal: stopped (<2 deg/s) s']=aft[aft.r.abs()<2].t.min()-53.1; r['pedal: height min/max']=f"{w.alt.min()-h1:+.2f}/{w.alt.max()-h1:+.2f}"; r['pedal: side drift max m/s']=w.vSide.abs().max()
    w=d[(d.t>=65)&(d.t<90)]; h2=d[(d.t>63)&(d.t<65)].alt.mean(); um=w.uFwd.max(); aft=w[w.t>78.1]
    r['fwd: speed max kt']=um*1.944; r['fwd: t to 90 % s']=w[w.uFwd>=0.9*um].t.iloc[0]-65.1; r['fwd: pitch min/max deg']=f"{w.theta.min():.1f}/{w.theta.max():.1f}"; r['fwd: pitch rate max deg/s']=w.q.abs().max()
    r['fwd: stop to 0.3 m/s s']=aft[aft.uFwd.abs()<0.3].t.min()-78.1; r['fwd: back-run m/s']=min(0,aft.uFwd.min()); r['fwd: height min m']=w.alt.min()-h2; r['fwd: height max m']=w.alt.max()-h2; r['fwd: pitch-rate reversals >1 deg/s']=nrev(w.q.values,1.0)
    w=d[(d.t>=90)&(d.t<110)]; h3=d[(d.t>88.5)&(d.t<90)].alt.mean(); sp=np.hypot(w.vSide,w.uFwd); aft=w[w.t>95.1]; spa=np.hypot(aft.vSide,aft.uFwd)
    r['both: speed max kt']=sp.max()*1.944; r['both: stop to 0.3 m/s s']=aft[spa<0.3].t.min()-95.1; r['both: height min/max']=f"{w.alt.min()-h3:+.2f}/{w.alt.max()-h3:+.2f}"; r['both: bank/pitch max']=f"{w.phi.abs().max():.1f}/{w.theta.max():.1f}"
    w=d[(d.t>=109.9)&(d.t<125)]; x0=w.pos_x.iloc[0]; z0=w.pos_z.iloc[0]; dist=np.hypot(w.pos_x-x0,w.pos_z-z0); sp=np.hypot(w.vSide,w.uFwd)
    r['gust 3 m/s: drift max m']=dist.max(); r['gust: speed <0.3 m/s after s']=w[(w.t>110.1)&(sp<0.3)].t.min()-110; r['gust: off the spot at the end m']=dist.iloc[-1]; r['gust: height min/max']=f"{w.alt.min()-w.alt.iloc[0]:+.2f}/{w.alt.max()-w.alt.iloc[0]:+.2f}"
    w=d[(d.t>17)&(d.t<19.5)]
    for c,l in (('aHlevel','hover: stopping accel m/s2'),('aUpBody','hover: spare lift m/s2'),('trcVmax','hover: full-stick speed m/s'),('tiltRate','hover: side tilt rate deg/s'),('tiltRateU','hover: fore-aft tilt rate (last move) deg/s'),('wz','hover: fit fore-aft weight'),('mass','mass kg')):
        if c in w: r[l]=w[c].mean()
    w=d[d.t>17]
    for c in ('trcVmax','wz','tiltRate'):
        if c in w: r[f'largest one-tick step in {c}']=np.abs(np.diff(w[c].values)).max()
    w=d[(d.t>=65)&(d.t<82)]
    if 'tiltRateU' in w: r['fwd: tilt rate used max deg/s']=w.tiltRateU.max()
    return r
def V(o):
    d=pd.read_csv(f'{o}/V.csv'); r={}
    for t0,t1,lab in ((15.1,28,'climb'),(31.1,50,'sink')):
        w=d[(d.t>=t0)&(d.t<t1)]; hf=w.alt.iloc[-30:].mean(); sg=1 if lab=='climb' else -1
        bad=w[w.vs.abs()>=0.1]; r[f'{lab} release: vs at release']=w.vs.iloc[0]; r[f'{lab} release: settled (<0.1 m/s) s']=(bad.t.max()-t0) if len(bad) else 0.0
        r[f'{lab} release: reverse vs peak m/s']=(sg*w.vs).min(); r[f'{lab} release: past the final height m']=(sg*(w.alt-hf)).max(); r[f'{lab} release: vs reversals >0.05']=nrev(w.vs.values,0.05)
    w=d[(d.t>8.3)&(d.t<15)]; r['climb: vs peak']=w.vs.max(); r['climb: t to 90 % s']=w[w.vs>0.9*w.vs.max()].t.iloc[0]-8.3
    w=d[(d.t>28.1)&(d.t<31.1)]; r['sink: vs peak']=w.vs.min(); r['alt min after lift-off']=d[d.t>12].alt.min()
    return r
def M(o):
    d=pd.read_csv(f'{o}/M.csv'); r={}
    for t0,t1,lab in ((40,50,'30 kt bank'),(85,95,'38 kt bank')):
        w=d[(d.t>=t0)&(d.t<t1)]; h=d[(d.t>t0-1.5)&(d.t<t0)].alt.mean()
        r[f'{lab}: gs kt']=d[(d.t>t0-1)&(d.t<t0)].gs.mean(); r[f'{lab}: bank max deg']=w.phi.abs().max(); r[f'{lab}: sideslip max deg']=w.beta.abs().max(); r[f'{lab}: height min/max']=f"{w.alt.min()-h:+.2f}/{w.alt.max()-h:+.2f}"; r[f'{lab}: roll-rate max']=w.p.abs().max(); r[f'{lab}: roll-rate reversals >1']=nrev(w.p.values,1.0); r[f'{lab}: yaw-rate max']=w.r.abs().max()
    w=d[(d.t>=58)&(d.t<68)]; r['30 kt pedal: yaw peak']=w.r.abs().max(); r['30 kt pedal: sideslip max']=w.beta.abs().max()
    w=d[(d.t>=70)&(d.t<85)]; r['lever 30->38 kt: pitch min/max']=f"{w.theta.min():.1f}/{w.theta.max():.1f}"; r['lever 30->38 kt: height min/max']=f"{w.alt.min()-d[(d.t>68)&(d.t<70)].alt.mean():+.2f}/{w.alt.max()-d[(d.t>68)&(d.t<70)].alt.mean():+.2f}"
    return r
def S(o):
    d=pd.read_csv(f'{o}/S.csv'); r={}; a=d[d.alt>d.alt.iloc[0]+0.5]
    r['STO: lift-off (0.5 m) s after lever']=a.t.iloc[0]-6.3 if len(a) else np.nan; r['STO: ground run m']=np.hypot(d.pos_x,d.pos_z)[a.index[0]] if len(a) else np.nan
    for tt in (10,15,20,30): 
        x=d.iloc[(d.t-tt).abs().values.argmin()]; r[f'STO: at {tt} s gs kt / alt m / pitch']=f"{x.gs:.0f} / {x.alt:.1f} / {x.theta:.1f}"
    r['STO: pitch-rate reversals >1']=nrev(d[d.t>6.3].q.values,1.0); r['STO: alt min after 8 s']=d[d.t>8].alt.min()
    return r
def D(o):
    d=pd.read_csv(f'{o}/D.csv'); on=d[d.state==2]; r={}; t0=on.t.iloc[0]; h0=on.alt.iloc[0]
    def tat(v):
        w=on[on.ias<=v]; return (w.t.iloc[0]-t0) if len(w) else np.nan
    r['decel: to 200 km/h s']=tat(200); r['decel: to 100 km/h s']=tat(100); r['decel: to 50 km/h s']=tat(50); r['decel: gs < 1 kt s']=(on[on.gs<1].t.iloc[0]-t0) if (on.gs<1).any() else np.nan
    r['decel: alt min/max m']=f"{on.alt.min()-h0:+.1f}/{on.alt.max()-h0:+.1f}"; r['decel: pitch min/max']=f"{on.theta.min():.1f}/{on.theta.max():.1f}"; r['decel: vs min/max']=f"{on.vs.min():+.2f}/{on.vs.max():+.2f}"
    lo=on[on.ias<150]; r['decel under 150 km/h: alt span m']=lo.alt.max()-lo.alt.min(); r['decel under 150 km/h: vs min/max']=f"{lo.vs.min():+.2f}/{lo.vs.max():+.2f}"; r['decel under 150 km/h: pitch-rate reversals >1']=nrev(lo.q.values,1.0); r['decel under 150 km/h: nozzle reversals >0.03']=nrev(lo.swivel.values,0.03); r['decel: AoA max']=on.alpha.max()
    return r
tags=sys.argv[1:]; res={}
for t in tags:
    rr={}
    for f in (H,V,M,S,D):
        try: rr.update(f(t))
        except Exception as e: rr[f.__name__+' FAILED']=str(e)[:60]
    res[t.rstrip('/').split('/')[-1]]=rr
keys=[]; 
for t in res:
    for k in res[t]:
        if k not in keys: keys.append(k)
print(f"{'':52s}"+''.join(f"{t:>16s}" for t in res))
for k in keys:
    row=''
    for t in res:
        v=res[t].get(k,''); row+=f"{v:16.3f}" if isinstance(v,(float,np.floating)) else f"{str(v):>16s}"
    print(f"{k:52s}"+row)
