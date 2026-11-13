import sys,numpy as np,pandas as pd
sys.path.insert(0,'.'); from det import pushes
logs=['20261103_223711','20261103_225204','20261103_231000','20261103_232903','20261103_233737','20261104_011013','20261104_011409','20261104_090307','20261104_093717','20261104_101811']
rows=[]
for lg in logs:
    d,ev=pushes(f'logs/F22E_FCS_{lg}.csv'); D=pd.read_csv(f'logs/F22E_FCS_{lg}.csv',usecols=['t','alpha','r','ias','ceCapNsP','ceCapNsN','ceCapZeP','ceCapZeN','sr','beta'])
    for i in ev:
        t0=d.t[i]; s=np.sign(d.r[i]); pre=D[(D.t>=t0-0.3)&(D.t<=t0)]
        w=D[(D.t>=t0)&(D.t<=t0+3)].reset_index(drop=True)
        if abs(w.r[0])<25 or not (w.alpha.abs()<10).any(): continue
        ia=np.argmax(w.alpha.abs().values<10); T=w.t[ia]-w.t[0]
        cns=(pre.ceCapNsN if s>0 else pre.ceCapNsP).mean(); cze=(pre.ceCapZeN if s>0 else pre.ceCapZeP).mean()
        fit=np.clip(0.028*d.ias[i]**1.5,6,80)
        rows.append(dict(log=lg[-6:],t=round(t0,1),ias=round(d.ias[i]),fit=fit,capNs=cns,capZe=cze,shed=s*(w.r[0]-w.r[ia])/T,bmax=w.beta.abs().max(),held=(w.sr.abs()>0.1).mean()>0.5))
r=pd.DataFrame(rows); r['fit_over_cap']=r.fit/r.capNs
r['iasb']=pd.cut(r.ias,[0,80,100,115,130,200])
print(r.groupby('iasb')[['fit','capNs','capZe','shed','fit_over_cap','bmax']].median().round(2)); print(r.groupby('iasb').size())
