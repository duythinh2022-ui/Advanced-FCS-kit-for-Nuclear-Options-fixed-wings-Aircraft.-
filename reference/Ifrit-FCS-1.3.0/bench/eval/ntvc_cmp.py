import pandas as pd,sys,glob,os,collections
D=sys.argv[1]; T=sys.argv[2].split(',')
def st(f):
    d=pd.read_csv(f); dt=d.t.iloc[1]-d.t.iloc[0]; n=os.path.basename(f)
    pull=d[d.sp.abs()>0.5]; t0=pull.t.min()
    s=d[d.t>=t0]
    out='a %5.1f..%5.1f'%(s.alpha.min(),s.alpha.max())
    if '_push' not in n:
        lim=s.alpha.max(); tl=s[s.alpha>lim-1].t.min()-t0
        out+=' t(lim-1) %.2f'%tl
        rel=d[(d.t>pull.t.max())]
        if len(rel):
            a0=rel.alpha.iloc[0]; tr=rel[rel.alpha<15].t.min()-rel.t.iloc[0] if (rel.alpha<15).any() else float('nan')
            out+=' rec->15 %.2f'%tr
    sr=s[s.sr.abs()>0.5]
    if len(sr):
        r0=sr.t.min(); w=s[(s.t>=r0)&(s.t<=sr.t.max())]
        out+=' |b| %4.1f bank2s %4.0f a(roll) %.1f..%.1f qsd %.1f'%(s[s.t>=r0].beta.abs().max(), s[(s.t>=r0)&(s.t<r0+2)].pS.sum()*dt, w[w.t>r0+0.5].alpha.min(), w.alpha.max(), w[w.t>r0+0.5].q.std())
    out+=' aDnMin %.0f'%s.aDn.min() if 'aDn' in s else ''
    return out
d=collections.defaultdict(dict)
for f in glob.glob(D+'/*.csv'):
    n=os.path.basename(f)[:-4]; tag,k=n.split('_',1); d[k][tag]=st(f)
for k in sorted(d, key=lambda k:(k.split('_')[0],int(k.split('_')[1]))):
    print('%-14s'%k); [print('   %-6s %s'%(t,d[k].get(t,'-'))) for t in T]
