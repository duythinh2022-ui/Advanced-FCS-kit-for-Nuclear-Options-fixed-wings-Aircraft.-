import sys
sys.path.insert(0,'/home/claude/w/fit')
from lib import runmany
cfgs=[c.split(',') if c else [] for c in sys.argv[1:]]
cases=[(i,t) for i in [250,350,500] for t in [1,0.3,0.0]]
for c in cfgs:
    res=runmany([[f"iasKmh={i}","prof=0:0,0.3:1,5:1,5.15:-0.6,9:-0.6","hold=5","trimT=1",f"throttle={t}","post=4","roll=0"]+c for i,t in cases])
    print("==",' '.join(c) or '(default)')
    print(" ias  thr | aHold  ias | push: qdotMin(0.6s)  t(a<=aHold-15)  qMin  aEnd | stab min/max  tvc min | aDn@push")
    for (i,t),D in zip(cases,res):
        if not D: print(i,t,"no data"); continue
        t0=D[0]['t']; tp=t0+5.0
        def at(tt): return min(D,key=lambda x:abs(x['t']-tt))
        h=at(tp-0.05); a0=h['alpha']
        P=[x for x in D if tp<=x['t']<=tp+4]
        qd=[(P[k+1]['q']-P[k]['q'])/(P[k+1]['t']-P[k]['t']) for k in range(len(P)-1) if P[k]['t']<=tp+0.6]
        tt=next((x['t']-tp for x in P if x['alpha']<=a0-15),float('nan'))
        print(f"{i:4d} {t:4.2f} | {a0:5.1f} {h['ias']:4.0f} | {min(qd):8.1f} {tt:10.2f} {min(x['q'] for x in P):9.1f} {P[-1]['alpha']:6.1f} | {min(min(x['stabL'],x['stabR']) for x in P):6.1f} {max(max(x['stabL'],x['stabR']) for x in P):6.1f} {min(x['tvc'] for x in P):6.1f} | {h['aDn']:5.1f}")
