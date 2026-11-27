# curve metrics per push; pushscore.py <tagA> <tagB>
import pandas as pd,numpy as np,glob,os,sys
A,B=sys.argv[1:3]; O='/home/claude/w/acc/out_push'
def revs(x,th):
    n=0;d=0;ref=x[0]
    for v in x:
        if d>=0 and v<ref-th: n+= d>0; d=-1; ref=v
        elif d<=0 and v>ref+th: n+= d<0; d=1; ref=v
        elif (d>=0 and v>ref) or (d<=0 and v<ref): ref=v
        if d==0 and abs(v-ref)>0: pass
    return n
def m(f):
    d=pd.read_csv(f); t0=d.t.iloc[0]+0.5; w=d[d.t>=t0]
    t20=(w[w.alpha_b<20].t.iloc[0]-t0) if (w.alpha_b<20).any() else np.nan
    t10=(w[w.alpha_b<10].t.iloc[0]-t0) if (w.alpha_b<10).any() else np.nan
    b=w[w.t<t0+(t20+0.5 if t20==t20 else 3)]
    tv=b.tvccmd_b.values; q=b.q_b.values; qc=b.q_cmd.values
    dq=np.diff(qc)
    spikes=int(np.sum((np.abs(dq[:-1])>8)&(np.abs(dq[1:])>8)&(dq[:-1]*dq[1:]<0)))
    # q curve smoothness: largest rate-of-push slow-down after the rate has been established (deg/s)
    qn=-q; peak=np.maximum.accumulate(qn); retreat=float((peak-qn).max())
    return dict(a0=w.alpha_b.iloc[0],beta=float(w.beta_b.abs().max()),t20=t20,t10=t10,noz=revs(tv,2),qrev=revs(q,3),spk=spikes,ret=retreat,qmin=float(q.min()),amin=float(w.alpha_b.min()))
fa=sorted(glob.glob(f'{O}/{A}/*.csv')); R=[]
for f in fa:
    g=f'{O}/{B}/'+os.path.basename(f)
    if not os.path.exists(g): continue
    a=m(f); b=m(g); R.append(dict(case=os.path.basename(f)[:-4],**{k+'_A':v for k,v in a.items()},**{k+'_B':v for k,v in b.items()}))
R=pd.DataFrame(R); R.to_csv(f'{O}/cmp_{A}_{B}.csv',index=False)
print(len(R),'pushes; A=',A,'B=',B)
for k in ['beta','t20','t10','noz','qrev','spk','ret']:
    print(f"{k:5s} A sum/med/max {np.nansum(R[k+'_A']):8.1f} {np.nanmedian(R[k+'_A']):6.2f} {np.nanmax(R[k+'_A']):6.2f} | B {np.nansum(R[k+'_B']):8.1f} {np.nanmedian(R[k+'_B']):6.2f} {np.nanmax(R[k+'_B']):6.2f}")
dt=R.t20_B-R.t20_A; db=R.beta_B-R.beta_A
print('t20 B-A: min/med/max',np.nanmin(dt).round(2),np.nanmedian(dt).round(2),np.nanmax(dt).round(2),' NaN A/B',R.t20_A.isna().sum(),R.t20_B.isna().sum())
print('beta B-A: min/med/max',db.min().round(1),db.median().round(1),db.max().round(1),'; worse >1:',(db>1).sum(),'better >1:',(db<-1).sum())
print(R.assign(db=db,dt=dt).sort_values('db',ascending=False).head(8)[['case','a0_A','beta_A','beta_B','t20_A','t20_B','noz_A','noz_B','ret_A','ret_B']].round(2).to_string(index=False))
print(R.assign(dt=dt).sort_values('dt',ascending=False).head(6)[['case','a0_A','t20_A','t20_B','beta_A','beta_B','ret_A','ret_B']].round(2).to_string(index=False))
