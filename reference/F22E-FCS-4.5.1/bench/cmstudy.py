import subprocess,re,sys,numpy as np
from concurrent.futures import ThreadPoolExecutor
D=np.arange(-30,35.1,2.5)
def sweep(ex,d):
    out=subprocess.run(['dotnet','bin/bench.dll','cmsweep','ias=300',f'stab={d}','a0=0','a1=60','da=2.5']+ex,capture_output=True,text=True).stdout
    return [(float(m.group(1)),float(m.group(2))) for m in re.finditer(r'alpha\s+(\S+)\s+pitchAcc\s+(\S+)',out)]
def study(name,ex):
    with ThreadPoolExecutor(4) as e: R=list(e.map(lambda d:sweep(ex,d),D))
    A=np.array([a for a,_ in R[0]]); M=np.array([[m for _,m in r] for r in R]).T  # M[alpha_i, delta_j]
    rows=[]
    for i,a in enumerate(A):
        if a not in (10,20,30,40,45,50): continue
        m=M[i]; f=lambda x:np.interp(x,D,m)
        # trim: zero crossing nearest 0
        s=np.sign(m); idx=np.where(np.diff(s)!=0)[0]
        if len(idx): k=idx[np.argmin(abs(D[idx]))]; dt=D[k]-m[k]*(D[k+1]-D[k])/(m[k+1]-m[k])
        else: dt=np.nan
        d0=dt if not np.isnan(dt) else 0
        asym=0.5*(f(d0+10)+f(d0-10))-f(d0)
        rows.append((a,dt,m.min()-f(d0),m.max()-f(d0),asym,f(0)))
    print(name); print('  AoA  stabTrim  noseDownAvail  noseUpAvail  split+-10 net  M(stab 0)')
    for r in rows: print('  %4.0f  %7.1f  %10.0f  %10.0f  %10.1f  %9.1f'%r)
V=[('current: peak 25, k 0.6',[]),
   ('earlier stall: peak 18',['StabStallAoA=18']),
   ('earlier stall: peak 15',['StabStallAoA=15']),
   ('later stall, same slope: peak 35 x1.4',['StabStallAoA=35','StabCLScale=1.4']),
   ('later stall, same slope: peak 30 x1.2',['StabStallAoA=30','StabCLScale=1.2']),
   ('downwash 0.8',['StabDownwash=0.8'])]
for n,ex in V: study(n,ex)
