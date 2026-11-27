import subprocess,os,sys
import pandas as pd
from concurrent.futures import ThreadPoolExecutor
exec(open('ens2.py').read().split('def one')[0].replace("out_ens2","out_attr"))
B=W+'/f22/bench'
VV={'P':'','P_shift8.75':'StabPeakRef=44.75','P_noSched':'ForeLiftSchedule=0:1 AftLiftSchedule=0:1','P_peak25':'StabStallAoA=25','P_peak25_noSched':'StabStallAoA=25 ForeLiftSchedule=0:1 AftLiftSchedule=0:1'}
def one(v,n):
    ias,prof,rprof,tr,ex=C[n]; f=f"{S}/{v}_{n.replace(' ','_').replace('+','_').replace('@','')}.csv"
    subprocess.run(['dotnet','bin/bench.dll','one',f'iasKmh={ias}','alt=1500','trimT=1','pull=0','hold=0','roll=0',f'prof={prof}',f'rprof={rprof}',f'csv={f}']+ex.split()+VV[v].split(),cwd=B,capture_output=True,env=dict(os.environ,BENCH_DATA=B+'/data/airframe_cal.json'))
    d=pd.read_csv(f); pre=d[(d.t>tr-0.2)&(d.t<tr)]; return v,n,abs(pre.p.mean())
J=[(v,n) for v in VV for n in C]
with ThreadPoolExecutor(2) as ex: R=list(ex.map(lambda j:one(*j),J))
d=pd.DataFrame(R,columns=['v','case','p']); pd.set_option('display.width',220); print(d.pivot(index='case',columns='v',values='p').round(1).to_string())
