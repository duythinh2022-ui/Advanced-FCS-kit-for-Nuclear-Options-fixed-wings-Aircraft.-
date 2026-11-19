import itertools,subprocess,sys,os,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
negs=[float(x) for x in sys.argv[1].split(',')]; prefix=sys.argv[2]
ias=[160,220,300]; gam=[45,80]; thr=[0.7,1.0]; rl=[1,-1]
jobs=list(itertools.product(negs,ias,gam,thr,rl))
def run(j):
    n,i,g,t,r=j; tag=f'{prefix}_n{int(-n)}_i{i}_g{g}_t{t}_r{r}'
    if not os.path.exists(f'runs/{tag}.csv'):
        subprocess.run(['./cs.sh',tag,str(n),str(i),str(g),str(t),'25',str(r)]+sys.argv[3:])
    d=pd.read_csv(f'runs/{tag}.csv'); d=d[d.t>-25]
    b=d.beta.abs(); dep=d[b>12]
    m=d[d.alpha< -0.9*abs(n)]
    return dict(neg=n,ias0=i,g=g,thr=t,roll=r,bmax=b.max(),tdep=(dep.t.iloc[0]+25) if len(dep) else np.nan,
        iasdep=dep.ias.iloc[0] if len(dep) else np.nan, iasmin=m.ias.min() if len(m) else np.nan, alphamin=d.alpha.min())
with ThreadPoolExecutor(os.cpu_count()) as ex: R=list(ex.map(run,jobs))
R=pd.DataFrame(R); R.to_csv(f'{prefix}_res.csv',index=False)
pd.set_option('display.width',200)
print(R.groupby('neg').agg(n=('bmax','size'),dep=('tdep',lambda s:s.notna().sum()),bmax_med=('bmax','median'),bmax_max=('bmax','max'),iasmin=('iasmin','min')))
