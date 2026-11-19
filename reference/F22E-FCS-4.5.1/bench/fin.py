import itertools,subprocess,os,sys,pandas as pd,numpy as np,warnings; warnings.filterwarnings('ignore')
from concurrent.futures import ThreadPoolExecutor
# both fins gone; AoA cap X; full aft held 10 s; lateral input variants; scores sideslip / departure
tag=sys.argv[1]; ex=sys.argv[2:]
FIN='detach=Vstab_L+Rudder_L+Vstab_R+Rudder_R'
IN={'ped':'0:0,10:0','pulse':'0:0,3:0,3.05:0.5,4:0.5,4.05:0,10:0','roll':'0:0,3:0,3.05:1,5:1,5.05:0,10:0','rollheld':'0:0,2:0,2.05:-1,9:-1,9.05:0,10:0'}
XS=[int(x) for x in os.environ.get('XS','15,20,25,30,35,40,45,50,55,60,65').split(',')]
SP=[int(x) for x in os.environ.get('SP','200,300,400').split(',')]
INS=os.environ.get('INS',','.join(IN)).split(',')
TV=os.environ.get('TV','on,off').split(',')
TH=[float(x) for x in os.environ.get('TH','0.5,1.0').split(',')]
cases=list(itertools.product(TV,SP,TH,XS,INS))
def run(c):
  tv,ias,thr,X,inp=c; f=f'runs/fin_{tag}_{tv}_{ias}_{thr}_{X}_{inp}.csv'
  lim=[f'AoAPos={X}'] if tv=='on' else [f'NoTvcAoACapPos={X}',f'NoTvcThrCapPos={X}',f'AoAPos={X}']
  if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one',FIN,'AoANeg=-50',f'iasKmh={ias}','trimT=2','hold=10','post=0','rollFromPull=0','rollT=10',f'throttle={thr}','prof=0:1,10:1',f'rprof={IN[inp]}',f'yprof={"0:0,3:0,3.05:0.4,3.6:0.4,3.65:0,10:0" if inp=="ped" else "0:0,10:0"}',f'csv={f}',f'DisableTVC={"false" if tv=="on" else "true"}']+lim+ex,capture_output=True)
  try:
    d=pd.read_csv(f); d['t']=d.t+10; w=d[d.t>=0]
  except Exception as e: return dict(tv=tv,ias=ias,thr=thr,X=X,inp=inp,err=1)
  e=w[w.t>=7]
  dep=(w.beta.abs().max()>25) or (w.alpha.max()>X+15)
  return dict(tv=tv,ias=ias,thr=thr,X=X,inp=inp,aEnd=e.alpha.mean(),aMax=w.alpha.max(),bmax=w.beta.abs().max(),bEnd=e.beta.abs().mean(),dep=int(dep),iasEnd=w.ias.iloc[-1],tDep=(w[w.beta.abs()>15].t.iloc[0] if (w.beta.abs()>15).any() else np.nan))
with ThreadPoolExecutor(8) as x: R=pd.DataFrame(list(x.map(run,cases)))
R.to_csv(f'fin_{tag}.csv',index=False)
pd.set_option('display.width',250)
print(R.pivot_table(index=['tv','X'],columns=['inp'],values='bmax',aggfunc='max').round(1).to_string())
print(R.pivot_table(index=['tv','X'],columns=['inp'],values='dep',aggfunc='sum').to_string())
