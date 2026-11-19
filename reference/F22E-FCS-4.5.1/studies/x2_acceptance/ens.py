# ensembles (5 entry speeds, +-0.1 / 0.2 %) for the suite cases that changed most
import subprocess,os,sys,json
import pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
W='/home/claude/w'; S=W+'/acc/out_ens'; os.makedirs(S,exist_ok=True)
BD={'cur':W+'/base/bench','P':W+'/f22/bench'}
C={'pull-then-roll 400':(400,"0:0,0.05:1,6:1","0:0,0.3:0,0.35:1,2.5:1,2.55:0,6:0",''),
'push-then-pullthrough+roll 450':(450,"0:0,0.05:-1,1.4:-1,1.5:1,5:1","0:0,0.3:0,0.35:1,4:1,4.05:0,6:0",''),
'roll-then-pull-rev 400':(400,"0:0,0.5:0,0.55:1,6:1","0:0,0.05:1,1.5:1,1.55:-1,2.6:-1,2.65:0,6:0",''),
'1g roll rev 500':(500,"0:0,8:0","0:0,0.05:1,1.0:1,1.05:-1,2.0:-1,2.05:0,6:0",''),
'pull1.0 thr0.15 400':(400,"0:0,0.05:1,6:1","0:0,0.3:0,0.35:1,6:1",'throttle=0.15'),
'pull1.0 thr0.3 400':(400,"0:0,0.05:1,6:1","0:0,0.3:0,0.35:1,6:1",'throttle=0.3'),
}
def one(v,n,k):
    ias,prof,rprof,ex=C[n]; ia=ias*(1+k*0.001); B=BD[v]; f=f"{S}/{v}_{n.replace(' ','_').replace('+','_')}_{k}.csv"
    subprocess.run(['dotnet','bin/bench.dll','one',f'iasKmh={ia:.3f}','alt=1500','trimT=1','pull=0','hold=0','roll=0','post=7.5',f'prof={prof}',f'rprof={rprof}',f'csv={f}']+ex.split(),cwd=B,capture_output=True,env=dict(os.environ,BENCH_DATA=B+'/data/airframe_cal.json'))
    d=pd.read_csv(f); w=d[d.t>0]; return v,n,k,float(w.alpha.max()),float(w.beta.abs().max())
J=[(v,n,k) for v in BD for n in C for k in (-2,-1,0,1,2)]
with ThreadPoolExecutor(2) as ex: R=list(ex.map(lambda j:one(*j),J))
d=pd.DataFrame(R,columns=['v','case','k','aMax','beta']); pd.set_option('display.width',200)
print(d.groupby(['case','v'])[['aMax','beta']].agg(['min','median','max']).round(1).to_string())
