import subprocess,os,sys,itertools,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
tag=sys.argv[1]; extra=sys.argv[2:]
cases=[]
for side,ias,man in itertools.product(['B'],[250,300,450,600],['roll','pullroll','pedal','rollrev']):
    a=['detach=Vstab_L+Rudder_L+Vstab_R+Rudder_R',f'iasKmh={ias}','throttle=0.9']
    if man=='roll': a+=['pull=0','hold=0.1','roll=1','rollT=1.5','post=4']
    if man=='pullroll': a+=['pull=0.6','hold=3','rollFromPull=0.8','roll=-1','rollT=1.5','post=3']
    if man=='pedal': a+=['pull=0','hold=0.1','roll=0','yaw=1','rollT=2','post=3']
    if man=='rollrev': a+=['pull=0.3','hold=5','rollFromPull=0.5','roll=1','rollT=3','rollRev=1.2','post=1']
    cases.append((f'{side}_{ias}_{man}',a))
def run(c):
    k,a=c; f=f'runs/f2_{tag}_{k}.csv'
    if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50','trimT=2',f'csv={f}']+a+extra,capture_output=True)
    d=pd.read_csv(f); w=d[d.t>=-0.5]
    return dict(case=k,bmax=w.beta.abs().max(),bEnd=abs(w.beta.iloc[-1]),pmax=w.p.abs().max(),aMax=w.alpha.max(),qsd=w.q.diff().abs().mean()*60,dec=w.decel.max() if 'decel' in w else 0)
with ThreadPoolExecutor(2) as ex: R=pd.DataFrame(list(ex.map(run,cases)))
R.to_csv(f'fin2_{tag}.csv',index=False); print(R.round(1).to_string())
