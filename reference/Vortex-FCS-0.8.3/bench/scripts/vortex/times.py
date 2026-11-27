# stop / translation times per case. usage: times.py tag...
import sys, pandas as pd, numpy as np
def lat(d):
    t=d.t.values; vx=np.gradient(d.pos_x.values,t); vz=np.gradient(d.pos_z.values,t); ps=np.radians(d.psi.values)
    return vx*np.cos(ps)-vz*np.sin(ps), vx*np.sin(ps)+vz*np.cos(ps)
def first(d,m,t0):
    w=d[(d.t>=t0)&m]; 
    return (w.t.iloc[0]-t0) if len(w) else float('nan')
def last_above(d,x,thr,t0):
    w=d[(d.t>=t0)&(np.abs(x)>thr)]
    return (w.t.iloc[-1]-t0) if len(w) else 0.0
for tag in sys.argv[1:]:
    o=[]
    try:
        d=pd.read_csv(f'/tmp/lg/dec_{tag}.csv'); o.append(f"dec: 0->27kt {first(d,d.gs>=27,25.3):.1f}s, 30->1kt {last_above(d,d.gs,1,40.3):.1f}s, pitch max {d[d.t>40].theta.max():.0f}")
        d=pd.read_csv(f'/tmp/lg/cb_{tag}.csv'); o.append(f"cb: stop<1m/s {last_above(d,d.gs,1.944,33.3):.1f}s, bank {d[(d.t>25)].phi.min():.0f}..{d[d.t>25].phi.max():.0f}")
        d=pd.read_csv(f'/tmp/lg/sk4_{tag}.csv'); o.append(f"sk4: <1m/s {last_above(d,d.gs,1.944,25):.1f}s, bank max {d[d.t>25].phi.abs().max():.0f}")
        d=pd.read_csv(f'/tmp/lg/ys_{tag}.csv'); v,u=lat(d); o.append(f"ys: side<1m/s {last_above(d,v,1,37.8):.1f}s")
        d=pd.read_csv(f'/tmp/lg/t30_{tag}.csv'); v,u=lat(d); o.append(f"t30: side<1m/s {last_above(d,v,1,42.4):.1f}s, hdg change {(d.psi.iloc[-1]-d[d.t<40].psi.iloc[-1]):.0f}")
        d=pd.read_csv(f'/tmp/lg/bk_{tag}.csv'); w=d[(d.t>45)&(d.t<56)]; o.append(f"bk: bank {w.phi.min():.0f}..{w.phi.max():.0f}, p max {w.p.abs().max():.0f}")
        d=pd.read_csv(f'/tmp/lg/hov_{tag}.csv'); w=d[(d.t>20)&(d.t<32)]; o.append(f"hov: r max {w.r.abs().max():.0f}, crab gs max {d[(d.t>32)&(d.t<45)].gs.max():.1f}kt")
    except Exception as e: o.append('ERR '+str(e))
    print(f"{tag:10s} "+' | '.join(o))
