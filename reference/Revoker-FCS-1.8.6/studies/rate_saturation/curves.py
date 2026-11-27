# curve-level A/B over the suite's per-case logs (FCS log format). usage: curves.py <suitesDir> <block:reg|sw|cork> <tagA> <tagB> [tagC]
# per case, in the window where a stick is in: surface travel and reversals, roll-target retreats with the stick held, roll-rate dips,
# pitch-command hitches, roll onset delay.
import sys, glob, os, numpy as np, pandas as pd
base=sys.argv[1]; blk=sys.argv[2]; tags=sys.argv[3:]
def held(x, tol=0.02):   # samples where the stick has not moved for 0.15 s
    x=np.asarray(x,float); n=9
    mv=np.abs(pd.Series(x).diff().fillna(0)).rolling(n,min_periods=1).max().values
    return mv<tol
def metrics(f):
    d=pd.read_csv(f); d=d[d.t>=0].reset_index(drop=True); dt=d.t.diff().median()
    act=(d.sr.abs()>0.05)|(d.sp.abs()>0.05)
    if act.sum()<10: return None
    surf=[c[:-4] for c in d.columns if c.endswith('_now') and c[:-4]+'_cmd' in d.columns]
    tr=[]; rv=[]
    for s in surf:
        x=d[s+'_now'].values; v=np.gradient(x,d.t.values); tr.append(np.abs(v[act.values]).mean())
        # reversals: zig-zag turning points with more than 3 deg between them
        piv=x[0]; dirn=0; cnt=0
        for xi in x[act.values]:
            if dirn>=0 and xi<piv-3: 
                if dirn>0: cnt+=1
                dirn=-1; piv=xi
            elif dirn<=0 and xi>piv+3:
                if dirn<0: cnt+=1
                dirn=1; piv=xi
            elif (dirn>0 and xi>piv) or (dirn<0 and xi<piv): piv=xi
        rv.append(cnt)
    T=act.sum()*dt
    tg=d.pSTarget.values; p=d.p.values; hs=held(d.sr)&(d.sr.abs().values>0.5)&held(d.sp,0.05)
    # roll-target retreat with both sticks held: largest move of |target| back toward zero inside any 0.5 s
    w=int(0.5/dt); at=np.abs(tg); ret=0.0; dip=0.0
    ap=np.abs(p)
    for i in range(len(d)-w):
        if hs[i:i+w].all():
            ret=max(ret, at[i]-at[i:i+w].min() - max(0.0, at[i]-at[i+w]))   # a dip that comes back (not a steady decline)
            dip=max(dip, ap[i]-ap[i:i+w].min() - max(0.0, ap[i]-ap[i+w]))
    # pitch command hitches: with the pitch stick held, the pitch-rate command going back by more than it ends up (0.4 s windows)
    hp=held(d.sp,0.02)&(d.sp.abs().values>0.05); qT=d.qT.values; w2=int(0.4/dt); qh=0.0
    for i in range(len(d)-w2):
        if hp[i:i+w2].all():
            seg=qT[i:i+w2+1]; lo=min(seg[0],seg[-1]); hi=max(seg[0],seg[-1])
            qh=max(qh, max(lo-seg.min(), seg.max()-hi))
    # roll onset: time from the first roll-stick move to 30 % of the roll rate reached 0.6 s later
    on=np.nan; i0=np.argmax(d.sr.abs().values>0.5)
    if d.sr.abs().values[i0]>0.5:
        i6=min(len(d)-1,i0+int(0.6/dt)); ref=abs(p[i6]-p[i0])
        k=np.argmax(np.abs(p[i0:i6+1]-p[i0])>=0.3*ref) if ref>3 else 0
        on=k*dt
    return dict(travel=np.mean(tr), revs=np.sum(rv)/max(T,0.1), tgtRetreat=ret, pDip=dip, qHitch=qh, onset=on, pMean=ap[act.values].mean(), betaPk=d.beta.abs().max())
rows={}
for t in tags:
    for f in sorted(glob.glob(f'{base}/{t}/{blk}/{t}/*.csv')):
        if f.endswith('summary.csv') or f.endswith('_d.csv'): continue
        m=metrics(f)
        if m: rows.setdefault(os.path.basename(f)[:-4],{})[t]=m
keys=['travel','revs','tgtRetreat','pDip','qHitch','onset','pMean','betaPk']
out=[]
for c,v in rows.items():
    if all(t in v for t in tags): out.append([c]+[v[t][k] for k in keys for t in tags])
df=pd.DataFrame(out,columns=['case']+[f'{k}:{t}' for k in keys for t in tags]).set_index('case')
pd.set_option('display.width',320); pd.set_option('display.max_rows',200)
print(f'== {blk}: {len(df)} cases; mean over cases'); print(df.mean().round(2).to_string())
a,b=tags[0],tags[-1]
for k,thr in [('travel',5),('revs',0.5),('tgtRetreat',3),('pDip',3),('qHitch',3),('onset',0.05)]:
    dd=(df[f'{k}:{b}']-df[f'{k}:{a}'])
    worse=dd[dd>thr]; better=dd[dd<-thr]
    print(f'-- {k}: worse by >{thr} in {len(worse)} cases, better in {len(better)}'); 
    if len(worse): print(df.loc[worse.index,[f'{k}:{t}' for t in tags]].round(2).to_string())
df.round(2).to_csv(f'/home/claude/work/runs/curves_{blk}_{"_".join(tags)}.csv')
