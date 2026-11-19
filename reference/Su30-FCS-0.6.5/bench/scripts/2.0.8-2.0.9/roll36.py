import sys,statistics as st
sys.path.insert(0,'/home/claude/w/fit')
from lib import runmany
cfgs=[c.split(',') if c else [] for c in sys.argv[1:]]
cases=[(i,t,tv) for i in (300,400) for t in (0,1) for tv in (0,1)]
for c in cfgs:
    res=runmany([[f"iasKmh={i}","pull=1","hold=8","rollFromPull=3","roll=1","rollT=4","trimT=1",f"throttle={t}","post=0","AoAPos=36","AoACap36=true"]+(["DisableTVC=true"] if tv else [])+c for i,t,tv in cases])
    print("==",' '.join(c) or 'default')
    for (i,t,tv),D in zip(cases,res):
        R=[x for x in D if abs(x['sr'])>0.5]
        if not R: print(i,t,tv,'none'); continue
        rate=[abs(R[k+1]['stabL']-R[k]['stabL'])/(R[k+1]['t']-R[k]['t']) for k in range(len(R)-1)]
        lmax=max(max(abs(x['sL_laoa']),abs(x['sR_laoa'])) for x in R)
        ps=[x['p'] for x in R if x['t']>R[0]['t']+1]
        print(f"  {i} thr{t} {'noTVC' if tv else 'TVC  '}: alpha {st.mean(x['alpha'] for x in R):5.1f} | p mean {st.mean(ps):5.1f} sd {st.pstdev(ps):5.1f} | stabL rate rms {(st.mean(r*r for r in rate))**0.5:5.1f} | stab |laoa| max {lmax:5.1f} | stab range {min(x['stabL'] for x in R):5.1f}..{max(x['stabL'] for x in R):5.1f}")
