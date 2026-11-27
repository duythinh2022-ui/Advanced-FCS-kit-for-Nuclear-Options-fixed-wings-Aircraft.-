import subprocess,json,os,sys
import numpy as np
from concurrent.futures import ThreadPoolExecutor
B=os.environ.get('BENCH_DIR','.')
BASE=os.environ.get('AFBASE','')
P1='StabDownwash=0 StabStallAoA=36 BodyLiftFore=1 BodyLiftAft=1'
AL=list(range(0,52,2))
def run(args):
    out=subprocess.run(['dotnet','bin/bench.dll']+args.split(),capture_output=True,text=True,cwd=B).stdout
    t=[l for l in out.splitlines() if l.startswith('TOTAL')][0].split(',')
    return float(t[4]),float(t[7])
def curve(cfg):
    js=[f'parts {BASE} {P1} {cfg} alpha={a} tas=85 rho=1.2 hl={min(1,max(0,(a-2)/18)) if a>0 else 0:.3f}' for a in AL]
    with ThreadPoolExecutor(8) as ex: R=list(ex.map(run,js))
    return np.array([r[0] for r in R]),np.array([r[1] for r in R])
basis={'cg+0.1':'CgShiftAft=0.15','lerxKv1':'LerxVortexKv=1','aftWin0.5':'BodyAftBump=0.5 BumpA0=6 BumpA1=16 BumpA2=34 BumpA3=50','fore1.2':'BodyLiftFore=1.2','aft1.2':'BodyLiftAft=1.2'}
N0,q0=curve('')
res={'base':(N0.tolist(),q0.tolist())}
for k,c in basis.items():
    N,q=curve(c); res[k]=(N.tolist(),q.tolist())
json.dump({'AL':AL,'res':res},open(sys.argv[1],'w'))
print('done')
