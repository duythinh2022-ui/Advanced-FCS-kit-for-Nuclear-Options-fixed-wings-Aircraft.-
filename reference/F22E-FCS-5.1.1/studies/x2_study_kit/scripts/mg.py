import subprocess,sys,json,os
from concurrent.futures import ThreadPoolExecutor
B=os.environ.get('BENCH_DIR','.')
BASE=os.environ.get('AFBASE','')
def run(args):
    out=subprocess.run(['dotnet','bin/bench.dll']+args.split(),capture_output=True,text=True,cwd=B).stdout
    t=[l for l in out.splitlines() if l.startswith('TOTAL')][0].split(',')
    return float(t[4]),float(t[7])
def margin(cfg,al=range(0,52,2),tas=85):
    I=float(subprocess.run(['dotnet','bin/bench.dll','inertia']+(BASE+' '+cfg).split(),capture_output=True,text=True,cwd=B).stdout.splitlines()[1].split()[0])
    js=[f'parts {BASE} {cfg} alpha={a} tas={tas} rho=1.2 hl={min(1,max(0,(a-2)/18)) if a>0 else 0:.3f}' for a in al]
    with ThreadPoolExecutor(8) as ex: R=list(ex.map(run,js))
    al=list(al); out={}
    for i in range(1,len(al)-1):
        (Np,qp),(Nm,qm)=R[i+1],R[i-1]
        out[al[i]]=(-(qp-qm)/57.2958*I/(Np-Nm), R[i][1])
    return out
if __name__=='__main__':
    m=margin(' '.join(sys.argv[2:]))
    print(sys.argv[1],' '.join(f'{a}:{v[0]:+.2f}' for a,v in m.items() if a%4==0 or a in (34,38,42)))
