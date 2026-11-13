import pandas as pd, numpy as np, sys, glob, os
pd.set_option('display.width',300); pd.set_option('display.max_rows',200)
# bench/an/asymmat.py <tag> [<tag> ...]: per case, after t = 6 s: AoA range, limit range (asymCapEff; the negative one for pushes), raw-cap range and
# jumps (>10 deg inside 0.5 s), AoA past the limit, IAS range, sideslip max, flight-path roll rate with the roll stick centred (mean, max),
# unmet roll (mean), roll rate max, g max.
OUT=os.environ.get('BENCH_OUT') or os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','out')
rows=[]
for tag in sys.argv[1:]:
    for f in sorted(glob.glob(OUT + "/" + os.environ.get("OUTSUB", "mat") + f"/{tag}/*.csv")):
        d=pd.read_csv(f,low_memory=False); n=os.path.basename(f)[:-4]
        x=d[d.t>6].copy()
        a=np.radians(x.alpha); x['vv']=x.p*np.cos(a)+x.r*np.sin(a)
        c=x.asymCapRaw.values; j=np.abs(c[25:]-c[:-25])>10; jumps=int((j[1:]&~j[:-1]).sum()+(1 if len(j) and j[0] else 0))
        cen=x[x.sr.abs()<0.05]
        push = 'push' in n
        lim = x.asymCapNeg if push else x.asymCapEff
        over=(x.alpha-lim).min() if push else (x.alpha-lim).max()
        rows.append(dict(case=n,tag=tag,aLo=x.alpha.min(),aHi=x.alpha.max(),limLo=lim.min(),limHi=lim.max(),rawLo=x.asymCapRaw.min(),rawHi=x.asymCapRaw.max(),jumps=jumps,over=over,iasLo=x.ias.min(),iasHi=x.ias.max(),bMax=x.beta.abs().max(),drift=cen.vv.abs().mean() if len(cen) else np.nan,driftMax=cen.vv.abs().max() if len(cen) else np.nan,unR=(cen.unR.abs().mean() if 'unR' in cen else np.nan) if len(cen) else np.nan,pMax=x.p.abs().max(),nzHi=x.nz.max()))
r=pd.DataFrame(rows).set_index(['case','tag']).sort_index()
print(r.round(1).to_string())
