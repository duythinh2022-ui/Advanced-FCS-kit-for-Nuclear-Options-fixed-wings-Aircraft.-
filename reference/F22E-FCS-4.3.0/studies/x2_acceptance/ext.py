import subprocess,os,sys,json
from concurrent.futures import ThreadPoolExecutor
W='/home/claude/w'; S=W+'/acc/out_x'; os.makedirs(S,exist_ok=True)
BD={'cur':W+'/base/bench','P':W+'/f22/bench'}
def one(v,tag,args):
    B=BD[v]; f=f'{S}/{v}_{tag}.csv'
    subprocess.run(['dotnet','bin/bench.dll','one']+args.split()+[f'csv={f}'],cwd=B,capture_output=True,env=dict(os.environ,BENCH_DATA=B+'/data/airframe_cal.json'))
J=[]
base='alt=1500 trimT=1 pull=0 hold=0 roll=0 post=8'
DM={'stabL':'detach=Elevator_L','tvcoff':'DisableTVC=true','stabL_tvcoff':'detach=Elevator_L DisableTVC=true','fins':'detach=Vstab_L+Vstab_R'}
for dn,dk in DM.items():
  for ias in (300,500,800):
    J.append((f'd_{dn}_{ias}_pull',f'{base} iasKmh={ias} throttle=0.75 prof=0:0,0.05:0.6,2:0.6,2.05:0,8:0 rprof=0:0,8:0 {dk}'))
    J.append((f'd_{dn}_{ias}_rel',f'{base} iasKmh={ias} throttle=0.75 prof=0:0,0.05:1,1.5:1,1.55:0,8:0 rprof=0:0,0.3:0,0.35:0.6,1.2:0.6,1.25:0,8:0 {dk}'))
for ias in (300,500,700):
  for name,prof in (('ramp','0:0,0.05:0.35,1:0.35,5:-0.35,7:-0.35'),('step','0:0,0.05:0.35,1:0.35,1.05:-0.7,5:-0.7'),('full','0:0,0.05:1,1:1,1.05:-1,4:-1')):
    J.append((f'z_{name}_{ias}',f'{base} iasKmh={ias} throttle=1 prof={prof} rprof=0:0,8:0'))
jobs=[(v,t,a) for v in BD for t,a in J]
with ThreadPoolExecutor(2) as ex: list(ex.map(lambda j:one(*j),jobs))
# static moment through zero
R={}
def parts(v,a):
    B=BD[v]; hl=min(1,max(0,(a-2)/18)) if a>0 else 0
    o=subprocess.run(['dotnet','bin/bench.dll','parts']+f'alpha={a} tas=85 rho=1.2 hl={hl:.3f}'.split(),capture_output=True,text=True,cwd=B,env=dict(os.environ,BENCH_DATA=B+'/data/airframe_cal.json')).stdout
    t=[l for l in o.splitlines() if l.startswith('TOTAL')][0].split(','); return float(t[4]),float(t[7])
for v in BD: R[v]={a:parts(v,a) for a in [-12,-10,-8,-6,-4,-3,-2,-1,0,1,2,2.8125,3,4,5,5.625,6,8,10,12]}
json.dump(R,open(S+'/static.json','w')); print('done',len(jobs))
