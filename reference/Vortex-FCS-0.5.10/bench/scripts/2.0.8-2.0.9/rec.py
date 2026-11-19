import sys
sys.path.insert(0,'/home/claude/w/fit')
from lib import runmany
cfgs=[c.split(',') if c else [] for c in sys.argv[1:]]
cases=[(i,t) for i in [250,350,500] for t in [1,0.3,0.0]]
for c in cfgs:
    res=runmany([[f"iasKmh={i}","pull=1","hold=5","trimT=1",f"throttle={t}","post=5","roll=0","pullRamp=0.2"]+c for i,t in cases])
    print("==",' '.join(c) or '(default)')
    print(" ias  thr | aMax  a@rel  stabMin(hold) tvcMin | after release: aMin  a@+2s qMin  stabMin | ias@rel")
    for (i,t),D in zip(cases,res):
        if not D: print(i,t,"no data"); continue
        H=[x for x in D if -5<=x['t']<=0]; P=[x for x in D if 0<x['t']<=5]
        def at(tt): return min(D,key=lambda x:abs(x['t']-tt))
        r=at(0)
        print(f"{i:4d} {t:4.2f} | {max(x['alpha'] for x in H):5.1f} {r['alpha']:5.1f} {min(min(x['stabL'],x['stabR']) for x in H):7.1f} {min(x['tvc'] for x in H):7.1f} | {min(x['alpha'] for x in P):6.1f} {at(2)['alpha']:6.1f} {min(x['q'] for x in P):6.1f} {min(min(x['stabL'],x['stabR']) for x in P):6.1f} | {r['ias']:4.0f}")
