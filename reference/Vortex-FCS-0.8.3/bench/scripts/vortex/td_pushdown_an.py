import pandas as pd,numpy as np,sys,re
for f in sys.argv[1:]:
    d=pd.read_csv(f); g=d[d.t<3].alt.mean()
    log=open(f.replace('.csv','.txt')).read(); m=re.search(r'static was about ([\d.]+)',log); stat=float(m.group(1)) if m else 0.12
    hc=g+stat
    air=(d.alt>hc+0.01).values
    # takeoff then landing: first airborne, then first contact after being >1 m up
    up=np.where(d.alt>hc+1.0)[0]
    if not len(up): print(f, 'never above 1 m'); continue
    after=d.iloc[up[0]:]
    c=after[after.alt<hc]
    if not len(c): print(f,'no landing'); continue
    i0=c.index[0]; t0=d.t[i0]; v0=d.vs[i0-1]
    a=d[d.t>=t0]; airA=(a.alt>hc+0.002).values; nb=int(np.sum(airA[1:]&~airA[:-1]))
    vmax=a[a.t<t0+1.5].vs.max()
    hi=d[(d.t<t0)&(d.t>t0-4)]
    dt=d.t.diff().median(); acc=np.gradient(d.vs.values,dt)
    # envelope entry: first tick in last 4 s where hdotCmd > -4.99 after being at -5 (or minimum)
    k=None
    for j in range(i0-1, i0-300, -1):
        if d.gsCmd is not None and d.hdotCmd[j-1] <= -4.99 and d.hdotCmd[j] > -4.99: k=j; break
    ent=f"entry {d.t[k]:.2f} vs {d.vs[k]:.2f} acc {acc[k]:+.2f}" if k else "no entry at -5"
    print(f"{f.split('/')[-1]:10s} peak alt {d.alt.max()-hc:5.2f} m | {ent} | contact t {t0:6.2f} at {v0:+.2f} m/s thrEng {d.thrEng[i0-1]:.2f} | wheels off {nb}x, highest {max(0,(a.alt-hc).max()):.3f} m, vs max after {vmax:+.2f}")
