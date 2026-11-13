import sys
sys.path.insert(0,'/home/claude/w/fit')
from lib import runmany
for nm,c in {'2.0.7':["StabDownwash=0","BodyLiftShaping=false"],'2.0.8':[]}.items():
    cs=[(i,p) for i in (300,450) for p in (-0.1,-0.2,-0.35,-0.6)]
    res=runmany([[f"iasKmh={i}",f"pull={p}","hold=4","trimT=1","throttle=1","post=0","roll=0","pullRamp=0.3"]+c for i,p in cs])
    print(nm)
    for (i,p),D in zip(cs,res):
        W=[x for x in D if -1.5<=x['t']<=0]; m=lambda k: sum(x[k] for x in W)/len(W)
        print(f"  {i} {p:5.2f}: alpha {m('alpha'):6.1f} nz {m('nz'):5.2f} stab {m('stabL'):5.1f} tvc {m('tvc'):5.1f} | aMin {min(x['alpha'] for x in D):6.1f}")
