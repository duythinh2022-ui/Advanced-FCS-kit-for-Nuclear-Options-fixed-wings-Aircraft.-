# flight set: cur = pristine 3.1.9 bench, X2s = study bench + X2.cfg, P = the ported 3.2.0 defaults
import subprocess,os
from concurrent.futures import ThreadPoolExecutor
W='/home/claude/w'; S=W+'/acc/out'; os.makedirs(S,exist_ok=True)
X2=open(W+'/kit/F22E-airframe-X2-handoff/configs/X2.cfg').read().strip()
V={'cur':(W+'/base/bench',''),'X2s':(W+'/study/bench',X2),'P':(W+'/f22/bench','')}
env=dict(os.environ)
def job(v,tag,args):
    B,c=V[v]; f=f'{S}/F_{v}_{tag}.csv'
    e=dict(env,BENCH_DATA=B+'/data/airframe_cal.json')
    subprocess.run(['dotnet','bin/bench.dll','one']+c.split()+args.split()+[f'csv={f}'],cwd=B,capture_output=True,env=e)
J=[]
for v in V:
    for ias,thr in [(400,1),(250,1),(250,0.75),(300,0),(250,0)]:
        J.append((v,f'lr_{ias}_{thr}',f'AoAPos=36 iasKmh={ias} gamma0=0 throttle={thr} trimT=3 hold=6 post=0 prof=0:1,6:1 rprof=0:0,1.5:0,1.55:1,6:1 rollFromPull=0'))
    for cap in [20,30,45]:
        for thr in [1,0]:
            J.append((v,f'hold{cap}_{thr}',f'AoAPos={cap} iasKmh=300 gamma0=0 throttle={thr} trimT=3 hold=6 post=0 prof=0:1,6:1 roll=0 rollFromPull=0 rprof=0:0'))
    J.append((v,'push_idle','AoAPos=45 iasKmh=300 gamma0=0 throttle=0 trimT=3 hold=7 post=0 prof=0:1,4:1,4.02:-1,7:-1 roll=0 rprof=0:0'))
    J.append((v,'push_notvc','AoAPos=45 DisableTVC=true iasKmh=300 gamma0=0 throttle=1 trimT=3 hold=7 post=0 prof=0:1,4:1,4.02:-1,7:-1 roll=0 rprof=0:0'))
    J.append((v,'push_ab','AoAPos=45 iasKmh=300 gamma0=0 throttle=1 trimT=3 hold=7 post=0 prof=0:1,4:1,4.02:-1,7:-1 roll=0 rprof=0:0'))
with ThreadPoolExecutor(2) as ex: list(ex.map(lambda j:job(*j),J))
print('done',len(J))
