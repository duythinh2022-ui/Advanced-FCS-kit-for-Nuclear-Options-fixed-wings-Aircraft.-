# three builds side by side: a = 0.3.15 (every switch off), b = 0.4.0, c = 0.4.1; /tmp/b3/<jet>_<a|b|c> are links to the result dirs
import pandas as pd, numpy as np
J=('bufref','stock','buflight','stk128','buf136','bufasym')
print("== stops (H case)")
print("jet       build | full stick: kt, t90 s, bank max, bank rate, stop<0.3 s, reverse m/s, back m, settled<0.15 s, height min/max | tap: reverse, back m, settled s | fwd stop: stop s, back-run, back m, settled s, height min/max, q max")
for tag in J:
    for m in ('a','b','c'):
        d=pd.read_csv(f'/tmp/b3/{tag}_{m}/H.csv')
        def seg(t0,t1,trel,col='vSide'):
            w=d[(d.t>=t0)&(d.t<t1)]; v=w[col].values; xs=np.cumsum(v)/60; aft=w[w.t>trel]
            sg=np.sign(v[np.argmax(np.abs(v))]); rev=max(0,-(sg*aft[col]).min()); back=np.abs(xs).max()-abs(xs[-1]); ts=aft[aft[col].abs()<0.3].t.min()-trel
            bad=aft[aft[col].abs()>0.15]; tset=(bad.t.max()-trel) if len(bad) else 0
            return np.abs(v).max()*1.944,ts,rev,back,tset
        a=seg(20,40,25.1); b=seg(40,50,41.6); f=seg(65,90,78.1,'uFwd')
        w=d[(d.t>=20)&(d.t<40)]; h0=d[(d.t>18.5)&(d.t<20)].alt.mean(); vm=w.vSide.abs().max(); t90=w[w.vSide.abs()>=0.9*vm].t.iloc[0]-20.1; br=np.abs(np.gradient(w.phi.values,1/60)).max()
        ff=d[(d.t>=65)&(d.t<90)]; h2=d[(d.t>63)&(d.t<65)].alt.mean()
        print(f"{tag:9s} {m:3s}  | {a[0]:5.1f}, {t90:.2f}, {w.phi.abs().max():.1f}, {br:.1f}, {a[1]:.2f}, {a[2]:.2f}, {a[3]:.2f}, {a[4]:.2f}, {w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f} | {b[2]:.2f}, {b[3]:.2f}, {b[4]:.2f} | {f[1]:.2f}, {f[2]:.2f}, {f[3]:.2f}, {f[4]:.2f}, {ff.alt.min()-h2:+.2f}/{ff.alt.max()-h2:+.2f}, {ff.q.abs().max():.1f}")
print("== both together / gust / hover values")
for tag in J:
    for m in ('a','b','c'):
        d=pd.read_csv(f'/tmp/b3/{tag}_{m}/H.csv')
        w=d[(d.t>=90)&(d.t<110)]; h3=d[(d.t>88.5)&(d.t<90)].alt.mean(); aft=w[w.t>95.1]; spa=np.hypot(aft.vSide,aft.uFwd)
        g=d[(d.t>=109.9)&(d.t<125)]; x0,z0=g.pos_x.iloc[0],g.pos_z.iloc[0]; dist=np.hypot(g.pos_x-x0,g.pos_z-z0)
        hv=d[(d.t>17)&(d.t<19.5)]; p=d[(d.t>=50)&(d.t<64)]
        print(f"{tag:9s} {m:3s}  | both: speed {np.hypot(w.vSide,w.uFwd).max()*1.944:.1f} kt, stop {aft[spa<0.3].t.min()-95.1:.2f} s, height {w.alt.min()-h3:+.2f}/{w.alt.max()-h3:+.2f}, vs {w.vs.min():+.2f}/{w.vs.max():+.2f} | gust: drift {dist.max():.1f} m, ends {dist.iloc[-1]:.1f} m off | hover: mass {hv.mass.mean():.0f}, spare {hv.aUpBody.mean():.2f}, aStop {hv.aHlevel.mean():.2f}, vmax {hv.trcVmax.mean()*1.944:.1f} kt, side tilt {hv.tiltRate.mean():.1f}, wz {hv.wz.mean():.3f} ({1/max(1e-3,hv.wz.mean()):.0f}:1), wz range {d[d.t>17].wz.min():.3f}-{d[d.t>17].wz.max():.3f} | pedal peak {p.r.abs().max():.1f}")
print("== climb / sink release (V)")
for tag in J:
    for m in ('a','b','c'):
        d=pd.read_csv(f'/tmp/b3/{tag}_{m}/V.csv'); out=f"{tag:9s} {m:3s}  |"
        for t0,t1,lab,sg in ((15.1,28,'climb',1),(31.1,50,'sink',-1)):
            w=d[(d.t>=t0)&(d.t<t1)]; bad=w[w.vs.abs()>=0.1]; hf=w.alt.iloc[-30:].mean()
            out+=f" {lab}: from {w.vs.iloc[0]:+.1f}, settled {((bad.t.max()-t0) if len(bad) else 0):.2f} s, reverse {(sg*w.vs).min():+.2f}, past final {(sg*(w.alt-hf)).max():.2f} m |"
        out+=f" alt min {d[d.t>20].alt.min():.1f}"
        print(out)
print("== 30 kt (M) and STO (S)")
for tag in J:
    for m in ('a','b','c'):
        d=pd.read_csv(f'/tmp/b3/{tag}_{m}/M.csv'); w=d[(d.t>=40)&(d.t<50)]; h=d[(d.t>38.5)&(d.t<40)].alt.mean(); p=d[(d.t>=58)&(d.t<68)]
        s=pd.read_csv(f'/tmp/b3/{tag}_{m}/S.csv'); a=s[s.alt>s.alt.iloc[0]+0.5]; x=s.iloc[(s.t-20).abs().values.argmin()]
        print(f"{tag:9s} {m:3s}  | 30 kt bank: max {w.phi.abs().max():.1f}, beta {w.beta.abs().max():.1f}, height {w.alt.min()-h:+.2f}/{w.alt.max()-h:+.2f}, p max {w.p.abs().max():.1f} | pedal: r {p.r.abs().max():.1f}, beta {p.beta.abs().max():.1f} | STO: lift-off {a.t.iloc[0]-6.3:.2f} s, run {np.hypot(s.pos_x,s.pos_z)[a.index[0]]:.1f} m, at 20 s {x.gs:.0f} kt {x.alt:.0f} m")
