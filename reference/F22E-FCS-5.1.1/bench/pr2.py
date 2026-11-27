import subprocess,sys,pandas as pd,os,numpy as np
from concurrent.futures import ThreadPoolExecutor
def swings(x,thr):
    n=0; ext=x[0]; d=0
    for v in x[1:]:
        if d>=0:
            if v>ext: ext=v
            elif ext-v>thr: n+= 1 if d!=0 else 0; d=-1; ext=v
        if d<0:
            if v<ext: ext=v
            elif v-ext>thr: n+=1; d=1; ext=v
    return n
tag=sys.argv[1]; ex=sys.argv[2:]
cases=[(ias,rd) for ias in [250,330,400,450,500,550,620,700] for rd in [0,0.5]]
def run(c):
  ias,rd=c; f=f'runs/pr2_{tag}_{ias}_{rd}.csv'
  if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50',f'iasKmh={ias}','trimT=1','hold=4','post=0','rollFromPull=0','rollT=4','throttle=1','prof=0:1,4:1',f'rprof=0:0,{rd}:0,{rd+0.05}:1,4:1',f'csv={f}']+ex,capture_output=True)
  d=pd.read_csv(f); d['t']=d.t+4; w=d[d.t>=0]; a=np.radians(w.alpha); ps=(w.p*np.cos(a)+w.r*np.sin(a)).values
  t20=w.t[w.alpha>20]; t40=w.t[w.alpha>40]
  return dict(kt=round(ias/1.852),rd=rd,bmax=w.beta.abs().max(),a2=w.alpha.iloc[min(len(w)-1,int(2/0.0167))],t40=t40.iloc[0] if len(t40) else np.nan,qsw=swings(w.q.values,10),psw=swings(ps,20),qmax=w.q.max(),bank2=w.phi.iloc[min(len(w)-1,int(2/0.0167))])
with ThreadPoolExecutor(2) as e: R=pd.DataFrame(list(e.map(run,cases)))
R.to_csv(f'pr2_{tag}.csv',index=False)
print(tag,'bmax mean %.1f worst %.1f | AoA@2s mean %.0f | t40 med %.2f | q swings %.2f max %d | roll swings %.2f max %d'%(R.bmax.mean(),R.bmax.max(),R.a2.mean(),R.t40.median(),R.qsw.mean(),R.qsw.max(),R.psw.mean(),R.psw.max()))
