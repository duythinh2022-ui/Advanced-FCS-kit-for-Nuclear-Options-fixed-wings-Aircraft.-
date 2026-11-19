import sys,pandas as pd,numpy as np
for f in sys.argv[1:]:
    d=pd.read_csv(f); d=d[d.t>=20].copy()
    # lateral / forward ground velocity in heading axes from position
    dt=d.t.diff(); vx=d.pos_x.diff()/dt; vz=d.pos_z.diff()/dt
    ps=np.radians(d.psi); d['u']=(vx*np.sin(ps)+vz*np.cos(ps)).rolling(5,center=True).mean(); d['v']=(vx*np.cos(ps)-vz*np.sin(ps)).rolling(5,center=True).mean()
    k=d[d.t>=25.1]; a0=d[(d.t>24)&(d.t<25)].alt.mean()
    t1=k[k.v.abs()<1.0].t.min(); t05=k[(k.t>(t1 if t1==t1 else 99))&(k.gs<0.5)].t.min()
    print(f"{f.split('/')[-1]:12s} v 25.1:{k.v.iloc[0]:5.1f}  |v|<1 at t {t1:5.1f}  gs<0.5 at {t05:5.1f}  phi {k.phi.min():5.1f}..{k.phi.max():5.1f}  vs {k.vs.min():5.2f}..{k.vs.max():5.2f}  alt dev {k.alt.min()-a0:5.2f}..{k.alt.max()-a0:5.2f}  psi {k.psi.min()-k.psi.iloc[0]:5.1f}..{k.psi.max()-k.psi.iloc[0]:5.1f}  r {k.r.abs().max():4.1f}  theta {k.theta.min():4.1f}..{k.theta.max():4.1f}  final gs {k.gs.iloc[-1]:.2f}")
    s=k[k.t.between(25,45)].iloc[::60]
    print("   t/v/phi/vs:", ' '.join(f"{a:.0f}/{b:.1f}/{c:.1f}/{e:.2f}" for a,b,c,e in zip(s.t,s.v,s.phi,s.vs)))
