import subprocess,os
import pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
W='/home/claude/w'; S=W+'/acc/out_ens2'; os.makedirs(S,exist_ok=True)
BD={'cur':W+'/base/bench','P':W+'/f22/bench'}
C={'0.5pull roll rev 400':(400,'0:0,0.05:0.5,7:0.5','0:0,1:0,1.05:1,3:1,3.05:-1,6:-1',3.05,'post=8.05 throttle=1'),
'0.5pull roll rev 556':(556,'0:0,0.05:0.5,7:0.5','0:0,1:0,1.05:1,3:1,3.05:-1,6:-1',3.05,'post=8.05 throttle=1'),
'0.5pull roll rev 700':(700,'0:0,0.05:0.5,7:0.5','0:0,1:0,1.05:1,3:1,3.05:-1,6:-1',3.05,'post=8.05 throttle=1'),
'pull+roll rev@0.7 650':(650,'0:0,0.05:1,7:1','0:0,0.05:1,0.7:1,0.75:-1,5:-1',0.75,'post=5.75 throttle=1'),
'held0.4+roll 350':(350,"0:0,0.05:0.4,6:0.4","0:0,1.2:0,1.25:1,3:1,3.05:0,6:0",3.0,'post=7.5'),
'pull-then-roll 320':(320,"0:0,0.05:1,6:1","0:0,0.3:0,0.35:1,2.5:1,2.55:0,6:0",2.5,'post=7.5')}
def one(v,n,k):
    ias,prof,rprof,tr,ex=C[n]; B=BD[v]; f=f"{S}/{v}_{n.replace(' ','_').replace('+','_').replace('@','')}_{k}.csv"
    subprocess.run(['dotnet','bin/bench.dll','one',f'iasKmh={ias*(1+k*0.001):.3f}','alt=1500','trimT=1','pull=0','hold=0','roll=0',f'prof={prof}',f'rprof={rprof}',f'csv={f}']+ex.split(),cwd=B,capture_output=True,env=dict(os.environ,BENCH_DATA=B+'/data/airframe_cal.json'))
    d=pd.read_csv(f); pre=d[(d.t>tr-0.2)&(d.t<tr)]
    return v,n,k,abs(pre.sp.mean() if False else pre.p.mean()),d[(d.t>0)&(d.t<tr)].p.abs().max(),pre.alpha.mean()
J=[(v,n,k) for v in BD for n in C for k in (-2,-1,0,1,2)]
with ThreadPoolExecutor(2) as ex: R=list(ex.map(lambda j:one(*j),J))
d=pd.DataFrame(R,columns=['v','case','k','p_pre','pMax','aoa']); pd.set_option('display.width',200)
print(d.groupby(['case','v'])[['p_pre','pMax','aoa']].median().round(1).unstack('v').to_string())
