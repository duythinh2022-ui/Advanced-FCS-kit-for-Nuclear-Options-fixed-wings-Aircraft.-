import pandas as pd,numpy as np,sys,glob,os
tags=sys.argv[1:]; R={}
for tag in tags:
  for f in glob.glob(f'rp/k_{tag}/*.csv'):
    n=os.path.basename(f)[:-4]; t=float(n.split('_')[1]); d=pd.read_csv(f)
    w=d[d.t>=t]; s=np.sign(w.alpha.iloc[0]); a=s*w.alpha.values; tt=w.t.values
    k=np.argmax(a<10) if (a<10).any() else len(a)-1
    seg=w.iloc[:k+1]; post=d[(d.t>=t)&(d.t<=tt[k]+1.0)]
    R.setdefault(n,{})[tag]=(tt[k]-t, seg.beta.abs().max(), post.beta.abs().max(), seg.ppQ.max())
print('push      '+''.join(f'{t:>26}' for t in tags)); print('          '+''.join(f'{"t10  bPush bPost ppQ":>26}' for t in tags))
for n in sorted(R,key=lambda x:(x[0],float(x.split('_')[1]))):
  print(f'{n:10}'+''.join(('%5.2f %5.1f %5.1f %5.1f'%R[n][t]).rjust(26) if t in R[n] else ' '*26 for t in tags))
for t in tags:
  v=np.array([R[n][t] for n in R if t in R[n]]); print(t,'mean t10 %.2f  bPush mean %.1f max %.1f  bPost mean %.1f max %.1f'%(v[:,0].mean(),v[:,1].mean(),v[:,1].max(),v[:,2].mean(),v[:,2].max()))
