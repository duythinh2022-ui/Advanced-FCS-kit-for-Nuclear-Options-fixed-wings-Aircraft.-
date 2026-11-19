import subprocess,sys,itertools,pandas as pd,numpy as np,os
from concurrent.futures import ThreadPoolExecutor
def run(tag,args,ov):
    f=f'runs/t_{tag}.csv'
    if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50']+args+ov+[f'csv={f}'],capture_output=True)
    return pd.read_csv(f)
variants={'K50P80':['SweepPosK=0.5','SweepPosPs0=80','SweepPosPsSpan=60'],'K50P100':['SweepPosK=0.5','SweepPosPs0=100','SweepPosPsSpan=60'],'K50P80b':['SweepPosK=0.5','SweepPosPs0=80','SweepPosPsSpan=40'],'K35':[],'K50':['SweepPosK=0.5'],'K70':['SweepPosK=0.7'],'K50L':['SweepPosK=0.5','SweepPosLag0=5','SweepPosLag1=12'],'K35P80':['SweepPosPs0=80','SweepPosPsSpan=60'],'off':['SweepPosLimitOn=false']}
def job(item):
    vn,ov=item; out={'v':vn}
    for ias in [300,450,600]:
        d=run(f'{vn}_rp{ias}',[f'iasKmh={ias}','trimT=3','hold=6','post=0','prof=0:0,1.49:0,1.5:1,6:1','rprof=0:1,6:1','rollFromPull=0','rollT=6','throttle=1'],ov)
        out[f'rp{ias}b']=d.beta.abs().max()
        d['t']=d.t+6; x=d[d.alpha>20]; out[f'rp{ias}t20']=x.t.iloc[0]-1.5 if len(x) else 9
    for ias in [550,700]:
        d=run(f'{vn}_pr{ias}',[f'iasKmh={ias}','trimT=1','pull=1','hold=4','rollFromPull=0','roll=1','rollT=4','throttle=1','post=0'],ov)
        d['t']=d.t+4; out[f'pr{ias}b']=d.beta.abs().max(); out[f'pr{ias}g1']=d.iloc[(d.t-1).abs().argmin()].nz
    return out
with ThreadPoolExecutor(2) as ex: R=list(ex.map(job,variants.items()))
pd.set_option('display.width',250); print(pd.DataFrame(R).round(2).to_string())
