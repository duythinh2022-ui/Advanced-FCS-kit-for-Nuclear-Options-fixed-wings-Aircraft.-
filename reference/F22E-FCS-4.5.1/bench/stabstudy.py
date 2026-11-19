import subprocess,re,sys,numpy as np
from concurrent.futures import ThreadPoolExecutor
pts=[(400,0.1),(300,0.25),(250,0.3),(220,0.45),(200,0.55)]
def run(args):
    var,ias,p,ex=args
    out=subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50',f'iasKmh={ias}','throttle=0.6','trimT=2','hold=5','post=0','rollFromPull=0','rollT=5',f'prof=0:{p},5:{p}','dumpAt=-2.3']+ex,capture_output=True,text=True).stdout
    a=float(re.search(r'DUMP t=\S+ alpha (\S+)',out).group(1))
    L=[l for l in out.splitlines() if 'Elevator_L ' in l][0]
    now=float(re.search(r'now\s+(\S+)',L).group(1))
    g=re.findall(r'(-?\d+):\s*(-?\d+)/\s*(-?\d+)/\s*(-?\d+)',L.split('|',1)[1].split('||')[0])
    d=np.array([float(x[0]) for x in g]); R=np.array([float(x[1]) for x in g]); P=np.array([float(x[2]) for x in g])
    M=lambda x:np.interp(x,d,P); Rr=lambda x:np.interp(x,d,R)
    return var,ias,a,now,P.min()-M(now),P.max()-M(now),M(now-10)+M(now+10)-2*M(now),(Rr(now-10)-Rr(now+10))/2
V={'V0 current (peak 25, k 0.6)':[],'V1 earlier stall (peak 18)':['StabStallAoA=18'],'V2 peak 15':['StabStallAoA=15'],'V3 downwash 0.8':['StabDownwash=0.8'],'V4 peak 35':['StabStallAoA=35'],'V5 dw 0.8 + peak 18':['StabDownwash=0.8','StabStallAoA=18']}
TV=len(sys.argv)>1
jobs=[]
for v,ex in V.items():
  for tvc in [True]:
    for ias,p in pts: jobs.append((v,ias,p,ex+([] if TV else ['DisableTVC=true'])))
with ThreadPoolExecutor(2) as e: res=list(e.map(run,jobs))
print('variant | AoA | stab trim | pitch dn avail | pitch up avail | split+-10 net pitch | roll per stab +-10')
for r in res: print('%-28s %5.1f %6.1f %7.0f %7.0f %7.1f %7.0f'%(r[0],r[2],r[3],r[4],r[5],r[6],r[7]))
