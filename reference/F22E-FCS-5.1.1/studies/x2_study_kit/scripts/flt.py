import subprocess,sys,os,csv,math
from concurrent.futures import ThreadPoolExecutor
B=os.environ.get('BENCH_DIR','.'); S=os.environ.get('OUT_DIR','.')
V={'cur':'','N':open(S+'/N.cfg').read().strip(),'T':open(S+'/T.cfg').read().strip(),'X2':open(S+'/X2.cfg').read().strip()}
def job(v,tag,args):
    f=f'{S}/F_{v}_{tag}.csv'
    subprocess.run(['dotnet','bin/bench.dll','one']+V[v].split()+args.split()+[f'csv={f}'],cwd=B,capture_output=True)
    return v,tag,f
J=[]
for v in V:
    for ias,thr in [(400,1),(250,1),(250,0.75),(300,0),(250,0)]:
        J.append((v,f'lr_{ias}_{thr}',f'AoAPos=36 iasKmh={ias} gamma0=0 throttle={thr} trimT=3 hold=6 post=0 prof=0:1,6:1 rprof=0:0,1.5:0,1.55:1,6:1 rollFromPull=0'))
    for cap in [20,30,45]:
        for thr in [1,0]:
            J.append((v,f'hold{cap}_{thr}',f'AoAPos={cap} iasKmh=300 gamma0=0 throttle={thr} trimT=3 hold=6 post=0 prof=0:1,6:1 roll=0 rollFromPull=0 rprof=0:0'))
    J.append((v,'push_idle',f'AoAPos=45 iasKmh=300 gamma0=0 throttle=0 trimT=3 hold=7 post=0 prof=0:1,4:1,4.02:-1,7:-1 roll=0 rprof=0:0'))
    J.append((v,'push_notvc',f'AoAPos=45 DisableTVC=true iasKmh=300 gamma0=0 throttle=1 trimT=3 hold=7 post=0 prof=0:1,4:1,4.02:-1,7:-1 roll=0 rprof=0:0'))
    J.append((v,'push_ab',f'AoAPos=45 iasKmh=300 gamma0=0 throttle=1 trimT=3 hold=7 post=0 prof=0:1,4:1,4.02:-1,7:-1 roll=0 rprof=0:0'))
with ThreadPoolExecutor(6) as ex: R=list(ex.map(lambda j:job(*j),J))
print('done',len(R))
