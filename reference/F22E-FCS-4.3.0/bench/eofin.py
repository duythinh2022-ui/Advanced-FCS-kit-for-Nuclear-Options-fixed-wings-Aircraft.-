import itertools,subprocess,os,sys,pandas as pd,numpy as np,warnings; warnings.filterwarnings('ignore')
from concurrent.futures import ThreadPoolExecutor
FIN='detach=Vstab_L+Rudder_L+Vstab_R+Rudder_R'
cases=list(itertools.product([250,400,600,800],[1.0,0.9,0.6,0.3],['on','off']))
def run(c):
    ias,thr,tv=c; f=f'runs/eofin_{ias}_{thr}_{tv}.csv'
    if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one',FIN,'engOutT=0.5',f'iasKmh={ias}','pull=0','hold=0.1','roll=0','rollT=0.1','post=10',f'throttle={thr}','trimT=2',f'csv={f}',f'DisableTVC={"false" if tv=="on" else "true"}','SplitFlaperonYawUse=1'],capture_output=True)
    d=pd.read_csv(f); w=d[d.t>=0]; e=w[w.t>=w.t.iloc[-1]-3]
    return dict(ias=ias,thr=thr,tv=tv,bmax=w.beta.abs().max(),bEnd=e.beta.abs().mean(),rEnd=e.r.abs().mean(),phiEnd=w.phi.iloc[-1],thrMin=w.thr.min(),dep=int(w.beta.abs().max()>25),iasEnd=w.ias.iloc[-1],decel=e.decel.mean() if 'decel' in e else np.nan)
with ThreadPoolExecutor(8) as x: R=pd.DataFrame(list(x.map(run,cases)))
pd.set_option('display.width',200); print(R.round(1).to_string(index=False))
