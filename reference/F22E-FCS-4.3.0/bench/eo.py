import subprocess,os,sys,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
tag=sys.argv[1]; extra=sys.argv[2:]
F1='detach=Vstab_R+Rudder_R'
cases={
 'eo_ab180':['engOutT=0.5','iasKmh=180','pull=0.3','hold=8','roll=0','rollT=0.1','post=0','throttle=1'],
 'eo_ab150':['engOutT=0.5','iasKmh=150','pull=0.5','hold=8','roll=0','rollT=0.1','post=0','throttle=1'],
 'eo_ab200_pull':['engOutT=0.5','iasKmh=200','pull=1','hold=6','roll=0','rollT=0.1','post=0','throttle=1'],
 'eo1fin_ab250L':['engOutT=0.5','iasKmh=250','pull=0.2','hold=8','roll=0','rollT=0.1','post=0','throttle=1',F1],
 'eo1fin_ab350L':['engOutT=0.5','iasKmh=350','pull=0.2','hold=8','roll=0','rollT=0.1','post=0','throttle=1',F1],
 'eo1fin_ab250R':['engOutT=0.5','iasKmh=250','pull=0.2','hold=8','roll=0','rollT=0.1','post=0','throttle=1','detach=Vstab_L+Rudder_L'],
}
def run(k):
    f=f'runs/e_{tag}_{k}.csv'
    if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50','trimT=2',f'csv={f}']+cases[k]+extra,capture_output=True)
    d=pd.read_csv(f); w=d[d.t>=-8]
    dec=w['decel'] if 'decel' in w else w.ias*0
    return dict(case=k,bmax=w.beta.abs().max(),bEnd=w.beta.iloc[-1],phiMax=w.phi.abs().max(),thrMin=w.thr.min(),tThrCut=(w.t[w.thr<0.98].iloc[0]+8) if (w.thr<0.98).any() else np.nan,split=((w.ailL-w.flapL).abs()+(w.ailR-w.flapR).abs()).max()/2,iasMin=w.ias.min(),alphaMax=w.alpha.max())
with ThreadPoolExecutor(2) as ex: R=pd.DataFrame(list(ex.map(run,cases)))
pd.set_option('display.width',200); print(R.round(1).to_string())
