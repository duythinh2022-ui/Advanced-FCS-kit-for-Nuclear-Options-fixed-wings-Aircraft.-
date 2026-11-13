import subprocess,sys
from concurrent.futures import ThreadPoolExecutor
B='/home/claude/w/src/bench'
def sweep(extra,stab):
    out=subprocess.run(['dotnet','bin/bench.dll','cmsweep','ias=300','a0=0','a1=45','da=5',f'stab={stab}']+extra,cwd=B,capture_output=True,text=True).stdout
    return {float(l.split()[1]):float(l.split()[3]) for l in out.splitlines() if l.startswith('alpha')}
stabs=list(range(-30,36,5))
for arg in sys.argv[1:]:
    c=arg.split(',')
    with ThreadPoolExecutor(6) as ex: Rl=list(ex.map(lambda s:sweep(c,s),stabs))
    R=dict(zip(stabs,Rl)); al=sorted(R[0])
    print(arg)
    print('   '+' '.join(f"{a:>13.0f}" for a in al))
    print(' N '+' '.join(f"{R[0][a]:13.1f}" for a in al))
    print(' D '+' '.join(f"{min(R[s][a] for s in stabs):13.1f}" for a in al))
    print(' U '+' '.join(f"{max(R[s][a] for s in stabs):13.1f}" for a in al))
