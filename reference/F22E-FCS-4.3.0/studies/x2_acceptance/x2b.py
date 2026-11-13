import subprocess,os
from concurrent.futures import ThreadPoolExecutor
import pandas as pd
W='/home/claude/w'; S=W+'/acc/out'; X=open(W+'/acc/X2b.args').read().strip()
B=W+'/f22/bench'
def job(tag,args):
    f=f'{S}/F_X2b_{tag}.csv'
    subprocess.run(['dotnet','bin/bench.dll','one']+X.split()+args.split()+[f'csv={f}'],cwd=B,capture_output=True,env=dict(os.environ,BENCH_DATA=B+'/data/airframe_cal.json'))
J=[]
for ias,thr in [(400,1),(250,1),(250,0.75),(300,0),(250,0)]:
    J.append((f'lr_{ias}_{thr}',f'AoAPos=36 iasKmh={ias} gamma0=0 throttle={thr} trimT=3 hold=6 post=0 prof=0:1,6:1 rprof=0:0,1.5:0,1.55:1,6:1 rollFromPull=0'))
for cap in [20,30,45]:
    for thr in [1,0]:
        J.append((f'hold{cap}_{thr}',f'AoAPos={cap} iasKmh=300 gamma0=0 throttle={thr} trimT=3 hold=6 post=0 prof=0:1,6:1 roll=0 rollFromPull=0 rprof=0:0'))
J.append(('push_idle','AoAPos=45 iasKmh=300 gamma0=0 throttle=0 trimT=3 hold=7 post=0 prof=0:1,4:1,4.02:-1,7:-1 roll=0 rprof=0:0'))
J.append(('push_notvc','AoAPos=45 DisableTVC=true iasKmh=300 gamma0=0 throttle=1 trimT=3 hold=7 post=0 prof=0:1,4:1,4.02:-1,7:-1 roll=0 rprof=0:0'))
J.append(('push_ab','AoAPos=45 iasKmh=300 gamma0=0 throttle=1 trimT=3 hold=7 post=0 prof=0:1,4:1,4.02:-1,7:-1 roll=0 rprof=0:0'))
with ThreadPoolExecutor(2) as ex: list(ex.map(lambda j:job(*j),J))
