import subprocess,os,sys,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
tag=sys.argv[1]; extra=sys.argv[2:]
FIN='detach=Vstab_L+Rudder_L+Vstab_R+Rudder_R'
cases={
 'fin_roll350':[FIN,'iasKmh=350','pull=0','hold=0.1','roll=1','rollT=1.5','post=4','throttle=0.8'],
 'fin_roll500':[FIN,'iasKmh=500','pull=0','hold=0.1','roll=1','rollT=1.5','post=4','throttle=0.8'],
 'fin_pull450':[FIN,'iasKmh=450','pull=0.5','hold=3','rollFromPull=1','roll=1','rollT=1.5','post=3','throttle=0.9'],
 'fin_ped400':[FIN,'iasKmh=400','pull=0','hold=0.1','roll=0','yaw=1','rollT=3','post=3','throttle=0.8','rollDelay=0'],
 'eo_ab250':['engOutT=0.5','iasKmh=250','pull=0','hold=0.1','roll=0','rollT=0.1','post=8','throttle=1'],
 'eo_ab330':['engOutT=0.5','iasKmh=330','pull=0','hold=0.1','roll=0','rollT=0.1','post=8','throttle=1'],
 'eo_ab450':['engOutT=0.5','iasKmh=450','pull=0','hold=0.1','roll=0','rollT=0.1','post=8','throttle=1'],
 'eo_mil250':['engOutT=0.5','iasKmh=250','pull=0','hold=0.1','roll=0','rollT=0.1','post=8','throttle=0.9'],
 'eo_roll330':['engOutT=0.5','iasKmh=330','pull=0','hold=0.1','rollDelay=3','roll=1','rollT=1.5','post=4','throttle=1'],
}
def run(k):
    f=f'runs/d_{tag}_{k}.csv'
    if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50','trimT=2',f'csv={f}']+cases[k]+extra,capture_output=True)
    d=pd.read_csv(f); w=d[d.t>=-0.5]
    split=((w.ailL-w.flapL).abs()+(w.ailR-w.flapR).abs()).max()/2
    return dict(case=k,bmax=w.beta.abs().max(),bEnd=w.beta.iloc[-1],phiEnd=w.phi.iloc[-1],pmax=w.p.abs().max(),rmax=w.r.abs().max(),thrMin=w.thr.min(),split=split,stabDiff=(w.stabL-w.stabR).abs().max(),iasEnd=w.ias.iloc[-1])
with ThreadPoolExecutor(2) as ex: R=pd.DataFrame(list(ex.map(run,cases)))
R.to_csv(f'dmg_{tag}.csv',index=False); pd.set_option('display.width',200); print(R.round(1).to_string())
