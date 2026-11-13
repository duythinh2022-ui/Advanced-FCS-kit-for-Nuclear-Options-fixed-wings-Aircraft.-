# bench/an/stopmat.py <tag> [<tag> ...]: per case of stopmat.sh, from the roll stick's release: IAS (kt), AoA, flight-path roll at the release, its peak after,
# time until under 5 deg/s, bank travelled in 3.5 s, peak sideslip; then the means per group.
import pandas as pd, numpy as np, sys, glob, os, warnings
warnings.filterwarnings('ignore')
tags=sys.argv[1:]; B=os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','out','stop','')
names=sorted(os.path.basename(f)[:-4] for f in glob.glob(B+tags[0]+'/*.csv'))
def m(tag,n):
    x=pd.read_csv(B+tag+'/'+n+'.csv',low_memory=False); x['vv']=x.p*np.cos(np.radians(x.alpha))+x.r*np.sin(np.radians(x.alpha))
    tr=float(n.split('_')[2])+1.52; d=1 if n.split('_')[1]=='P' else -1
    s=x[(x.t>=tr)&(x.t<=tr+3.5)]
    vv0=s.vv.iloc[0]*d; st=s[(s.vv*d)<5]; ts=(st.t.iloc[0]-tr) if len(st) else np.nan
    bank=np.trapezoid(s.vv*d,s.t); pk=(s.vv*d).max()
    return dict(ias=s.ias.iloc[0]*1.944,a0=s.alpha.iloc[0],vv0=vv0,pk=pk,ts=ts,bank=bank,b=s.beta.abs().max(),aMin=s.alpha.min(),aMax=s.alpha.max())
print(f"{'case':14s} " + " | ".join(f"{t:>6s}: kt  a0  vv0  peak tStop bank  bMax aMax" for t in tags))
rows={t:[] for t in tags}
for n in names:
    out=[]
    for t in tags:
        try:
            r=m(t,n); rows[t].append((n,r)); out.append(f"       {r['ias']:4.0f} {r['a0']:4.0f} {r['vv0']:4.0f} {r['pk']:5.0f} {r['ts']:5.2f} {r['bank']:5.0f} {r['b']:5.1f} {r['aMax']:4.0f}")
        except Exception as e: out.append(f"  ERR {e}")
    print(f"{n:14s} "+" | ".join(out))
for t in tags:
    for key,lab in (('R_P','load, heavy side'),('R_M','load, other side'),('N_','clean')):
        rs=[r for n,r in rows[t] if n.startswith(key)]
        if rs: print(f"{t:8s} {lab:18s}: bank mean {np.mean([r['bank'] for r in rs]):5.1f} max {np.max([r['bank'] for r in rs]):5.1f} | tStop mean {np.nanmean([r['ts'] for r in rs]):4.2f} (not stopped in 3.5 s: {sum(np.isnan(r['ts']) for r in rs)}) | sideslip max {np.max([r['b'] for r in rs]):4.1f}")
