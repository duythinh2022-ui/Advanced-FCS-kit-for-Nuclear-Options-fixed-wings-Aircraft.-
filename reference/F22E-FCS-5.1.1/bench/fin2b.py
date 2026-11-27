import subprocess,os,sys,itertools,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
tag=sys.argv[1]; extra=sys.argv[2:]
cases=[]
for ias,thr in [(150,0.1),(250,0.9),(300,0.9),(450,0.9),(600,0.9)]:
  for man in ['roll','pullroll','pedal','rollrev','rollpulse']:
    a=['detach=Vstab_L+Rudder_L+Vstab_R+Rudder_R',f'iasKmh={ias}',f'throttle={thr}']
    if man=='roll': a+=['pull=0','hold=0.1','roll=1','rollT=1.5','post=4']
    if man=='pullroll': a+=['pull=0.6','hold=3','rollFromPull=0.8','roll=-1','rollT=1.5','post=3']
    if man=='pedal': a+=['pull=0','hold=0.1','roll=0','yaw=1','rollT=2','post=3']
    if man=='rollrev': a+=['pull=0.3','hold=5','rollFromPull=0.5','roll=1','rollT=3','rollRev=1.2','post=1']
    if man=='rollpulse': a+=['pull=0','hold=6','rollFromPull=0','rprof=0:0,0.5:0,0.55:-1,1.5:-1,1.55:0,2.5:0,2.55:1,3.5:1,3.55:0,6:0','post=0']
    cases.append((f'{ias}_{man}',a))
def run(c):
    k,a=c; f=f'runs/f2b_{tag}_{k}.csv'
    if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50','trimT=2',f'csv={f}']+a+extra,capture_output=True)
    d=pd.read_csv(f); w=d[d.t>=d.t.iloc[0]+2.0]
    e=(w.p-w.pCmd)
    return dict(case=k,bmax=w.beta.abs().max(),bEnd=abs(w.beta.iloc[-1]),prms=np.sqrt((e**2).mean()),aMax=w.alpha.max(),qsd=w.q.diff().abs().mean()*60,tvcAct=w.tvc.diff().abs().mean()*60,flapAct=(w.flapL.diff().abs()+w.ailL.diff().abs()).mean()*60,dec=w.decel.mean() if 'decel' in w else 0)
with ThreadPoolExecutor(2) as ex: R=pd.DataFrame(list(ex.map(run,cases)))
R.to_csv(f'fin2b_{tag}.csv',index=False); print(R.round(1).to_string())
print(tag,'bmax mean %.1f worst %.1f prms %.1f qsd %.1f tvcAct %.1f flapAct %.0f'%(R.bmax.mean(),R.bmax.max(),R.prms.mean(),R.qsd.mean(),R.tvcAct.mean(),R.flapAct.mean()))
