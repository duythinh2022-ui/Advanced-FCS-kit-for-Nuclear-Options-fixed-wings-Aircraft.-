import subprocess,sys
from concurrent.futures import ThreadPoolExecutor
B='/home/claude/w/src/bench'
def sweep(args):
    out=subprocess.run(['dotnet','bin/bench.dll','cmsweep','ias=300','a0=-40','a1=50','da=5']+args,cwd=B,capture_output=True,text=True).stdout
    return {float(l.split()[1]):float(l.split()[3]) for l in out.splitlines() if l.startswith('alpha')}
extra=sys.argv[1].split(',') if len(sys.argv)>1 and sys.argv[1] else []
stabs=list(range(-30,36,5))
combos={'stab':[[]],'stab+rud20':[['rud=-20'],['rud=20']],'stab+rud35+flap':[['rud=-35','flap=-25','ail=-25'],['rud=35','flap=25','ail=25']]}
with ThreadPoolExecutor(8) as ex:
    for nm,cl in combos.items():
        jobs=[(s,c) for s in stabs for c in cl]
        R=list(ex.map(lambda j: sweep([f'stab={j[0]}']+j[1]+extra),jobs))
        al=sorted(R[0])
        dn={a:min(r[a] for r in R) for a in al}; up={a:max(r[a] for r in R) for a in al}
        print(f"{nm:16s} D "+' '.join(f"{a:.0f}:{dn[a]:.1f}" for a in al))
        print(f"{'':16s} U "+' '.join(f"{a:.0f}:{up[a]:.1f}" for a in al))
