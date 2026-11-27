import subprocess,sys,math,json
from concurrent.futures import ThreadPoolExecutor
import os
BASE=os.environ.get('AFBASE','MisStab=1 MisRud=1 MisWing=1 MisBody=1 IScalePitch=1 CgAftShift=0')
def run(args):
    out=subprocess.run(['dotnet','bin/bench.dll']+args.split(),capture_output=True,text=True,cwd=os.environ.get('BENCH_DIR','.')).stdout
    t=[l for l in out.splitlines() if l.startswith('TOTAL')][0].split(',')
    return float(t[4]),float(t[7]),float(t[6]),float(t[8])
DEFL=[-30,-20,-10,-5,0,5,10,20,35]
def analyse(cfg,alphas=range(0,52,2),tas=85):
    jobs=[]
    for a in alphas:
        hl=min(1,max(0,(a-2)/18)) if a>0 else 0
        for d in DEFL: jobs.append((a,d,f'parts {BASE} {cfg} alpha={a} tas={tas} rho=1.2 hl={hl:.3f} Elevator_L={d} Elevator_R={d}'))
    with ThreadPoolExecutor(6) as ex: res=list(ex.map(lambda j:run(j[2]),jobs))
    R={}
    for (a,d,_),r in zip(jobs,res): R.setdefault(a,{})[d]=r
    I=float(subprocess.run(['dotnet','bin/bench.dll','inertia']+(BASE+' '+cfg).split(),capture_output=True,text=True,cwd=os.environ.get('BENCH_DIR','.')).stdout.splitlines()[1].split()[0])
    rows=[]
    al=list(alphas)
    for i,a in enumerate(al):
        N0,q0=R[a][0][0],R[a][0][1]
        if 0<i<len(al)-1:
            Np,qp=R[al[i+1]][0][:2]; Nm,qm=R[al[i-1]][0][:2]
            np_=-(qp-qm)/57.2958*I/(Np-Nm) if abs(Np-Nm)>1 else float('nan')
        else: np_=float('nan')
        qs=[R[a][d][1] for d in DEFL]
        trim=float('nan')
        # trim: zero crossing nearest 0 deflection
        best=None
        for k in range(len(DEFL)-1):
            q1,q2=qs[k],qs[k+1]
            if (q1<=0<=q2) or (q2<=0<=q1):
                d=DEFL[k]+(DEFL[k+1]-DEFL[k])*(0-q1)/(q2-q1) if q2!=q1 else DEFL[k]
                if best is None or abs(d)<abs(best): best=d
        if best is not None: trim=best
        rows.append(dict(a=a,np=np_,q0=q0,trim=trim,dn=min(qs),up=max(qs),nz=N0/27500/9.81))
    return rows
def show(name,rows,sel=(0,4,10,16,20,24,28,32,36,40,44,48,52)):
    print(f'== {name}')
    print(' AoA |  NP aft CG (m) | stabs-0 pitch | trim stab | full-stab nose-dn / nose-up (deg/s^2) | nz')
    for r in rows:
        if r['a'] in sel: print(f"{r['a']:4d} | {r['np']:8.3f}       | {r['q0']:8.1f}      | {r['trim']:7.1f}   | {r['dn']:8.1f} / {r['up']:7.1f}  | {r['nz']:5.2f}")
if __name__=='__main__':
    name=sys.argv[1]; cfg=' '.join(sys.argv[2:])
    rows=analyse(cfg,alphas=range(0,56,2)); show(name,rows)
    json.dump(rows,open(os.path.join(os.environ.get('OUT_DIR','.'),f'af_{name}.json'),'w'))
