# height-hold metrics per case. usage: vsan.py <tag> [tag2 ...]
import sys, pandas as pd, numpy as np
W={'dec':(24.5,70),'cb':(24.5,70),'ys':(34.5,75),'t30':(39.5,70),'bk':(44.5,75),'hov':(19.5,62),'sk4':(24.5,60)}
BAND=0.2
def runs(mask,t):
    out=[];i=0;n=len(mask)
    while i<n:
        if mask[i]:
            j=i
            while j+1<n and mask[j+1]: j+=1
            out.append((t[i],t[j],i,j)); i=j+1
        else: i+=1
    return out
def an(case,tag):
    try: d=pd.read_csv(f'/tmp/lg/{case}_{tag}.csv')
    except Exception as e: return None
    a,b=W[case]; pre=d[(d.t>=a-1.5)&(d.t<a)]; h0=pre.alt.mean()
    w=d[(d.t>=a)&(d.t<=b)].reset_index(drop=True); t=w.t.values; vs=w.vs.values; e=w.alt.values-h0
    out=np.abs(vs)>BAND; rr=runs(out,t); dt=np.median(np.diff(t))
    tout=out.sum()*dt; longest=max([r[1]-r[0]+dt for r in rr],default=0); 
    lr=max(rr,key=lambda r:r[1]-r[0]) if rr else None
    ldir=('sink' if vs[lr[2]]<0 else 'rise') if lr else '-'
    # disturbance end: last time the horizontal acceleration > 0.3 m/s^2, |r| > 2 deg/s, |p| > 2 deg/s or the pilot inputs moved
    vx=np.gradient(w.pos_x.values,t); vz=np.gradient(w.pos_z.values,t)
    k=max(1,int(round(0.3/dt))); sm=lambda x: pd.Series(x).rolling(k,center=True,min_periods=1).mean().values
    ah=np.hypot(np.gradient(sm(vx),t),np.gradient(sm(vz),t)); ah=sm(ah)
    act=(ah>0.3)|(np.abs(w.r.values)>2)|(np.abs(w.p.values)>2)
    tend=t[np.where(act)[0][-1]] if act.any() else a
    def back(tol):
        bad=np.where(np.abs(e)>tol)[0]
        if len(bad)==0: return a
        if bad[-1]==len(e)-1: return np.nan
        return t[bad[-1]+1]
    b05=back(0.05); b10=back(0.10)
    return dict(case=case,h0=h0,tout=tout,n=len(rr),longest=longest,ldir=ldir,vmin=vs.min(),vmax=vs.max(),emin=e.min(),emax=e.max(),tend=tend,
                rec05=(b05-tend) if b05==b05 else np.nan, rec10=(b10-tend) if b10==b10 else np.nan, efin=e[-1],
                thrmax=w.thrEng.max(), tsat=(w.thrEng>=0.875).sum()*dt)
tags=sys.argv[1:]
for tag in tags:
    print(f"== {tag}")
    print(f"{'case':5s} {'out±0.2':>8s} {'n':>2s} {'longest':>9s} {'vs min/max':>12s} {'h err min/max':>14s} {'dist end':>8s} {'<0.05 after':>11s} {'<0.10 after':>11s} {'h end':>6s} {'thr max':>7s} {'sat s':>5s}")
    for c in W:
        r=an(c,tag)
        if r is None: print(f"{c:5s} (no data)"); continue
        f=lambda x: 'never' if x!=x else (f"{max(x,0):5.1f} s")
        print(f"{c:5s} {r['tout']:6.1f} s {r['n']:2d} {r['longest']:4.1f} {r['ldir']:4s} {r['vmin']:+.2f}/{r['vmax']:+.2f} {r['emin']:+6.2f}/{r['emax']:+.2f}  {r['tend']:6.1f}  {f(r['rec05']):>10s}  {f(r['rec10']):>10s} {r['efin']:+.2f}  {r['thrmax']:.2f}  {r['tsat']:4.1f}")
