import subprocess,sys,json,os
import numpy as np
from concurrent.futures import ThreadPoolExecutor
B=os.environ.get('BENCH_DIR','.')
BASE=os.environ.get('AFBASE','')
K=list(range(0,49,4))
def run(args):
    out=subprocess.run(['dotnet','bin/bench.dll']+args.split(),capture_output=True,text=True,cwd=B).stdout
    t=[l for l in out.splitlines() if l.startswith('TOTAL')][0].split(',')
    return float(t[4]),float(t[7])
def tab(ms): return ','.join(f'{a}:{m:.4f}' for a,m in zip(K,ms))
def curve(cfg,fm,am,al=K):
    js=[f'parts {BASE} {cfg} ForeSched={tab(fm)} AftSched={tab(am)} alpha={a} tas=85 rho=1.2 hl={min(1,max(0,(a-2)/18)) if a>0 else 0:.3f}' for a in al]
    with ThreadPoolExecutor(8) as ex: R=list(ex.map(run,js))
    return np.array([r[0] for r in R]),np.array([r[1] for r in R])
cfg=' '.join(sys.argv[2:]); name=sys.argv[1]
qtgt=float(os.environ.get('QTGT','0'))
fm=np.ones(len(K)); am=np.ones(len(K))
N0,q00=curve(cfg,fm,am)
for it in range(4):
    N,q=curve(cfg,fm,am)
    Nf,qf=curve(cfg,fm*1.1,am); Na,qa=curve(cfg,fm,am*1.1)
    for i,a in enumerate(K):
        if a==0: continue
        A=np.array([[qf[i]-q[i],qa[i]-q[i]],[Nf[i]-N[i],Na[i]-N[i]]])
        rhs=np.array([qtgt-q[i], N0[i]-N[i]])
        try: x=np.linalg.solve(A,rhs)
        except Exception: continue
        fm[i]*=1+0.1*x[0]; am[i]*=1+0.1*x[1]
        fm[i]=min(max(fm[i],0.2),3); am[i]=min(max(am[i],0.2),4)
    print(f'iter {it}: max|q| {np.max(np.abs(q[1:-1]-qtgt)):.2f}',flush=True)
N,q=curve(cfg,fm,am)
print(name,'ForeSched='+tab(fm),'AftSched='+tab(am))
print(' AoA  q_before  q_after  lift_change%  fore  aft')
for i,a in enumerate(K): print(f'{a:4d} {q00[i]:8.1f} {q[i]:8.1f} {100*(N[i]-N0[i])/max(1,abs(N0[i])):8.1f} {fm[i]:6.3f} {am[i]:6.3f}')
json.dump({'cfg':cfg,'fore':tab(fm),'aft':tab(am)},open(os.path.join(os.environ.get('OUT_DIR','.'),f'sched_{name}.json'),'w'))
