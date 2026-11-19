import sys
sys.path.insert(0,'/home/claude/w/fit')
from lib import runmany
cfgs={'2.0.7':["StabDownwash=0","BodyLiftShaping=false"],'2.0.8':["StabDownwashHigh=0.6","BodyLiftShaping=false"],'2.0.9':[]}
# note: 2.0.8 here = const 0.6 downwash with body OFF is not 2.0.8; use real 2.0.8 build separately
pulls=[0.35,0.40,0.44,0.47,0.50]
for nm,c in cfgs.items():
    if nm=='2.0.8': continue
    res=runmany([["iasKmh=350",f"pull={p}","hold=6","rollFromPull=3","roll=1","rollT=2","trimT=1","throttle=1","post=0"]+c for p in pulls])
    print(nm)
    for p,D in zip(pulls,res):
        pre=[x for x in D if x['sr']==0 and x['t']>-4 and x['t']<-3.1]
        R=[x for x in D if abs(x['sr'])>0.5]
        a=sum(x['alpha'] for x in R)/len(R)
        lmax=max(max(x['sL_laoa'],x['sR_laoa']) for x in R)
        aon=next((x['alpha'] for x in R if max(x['sL_laoa'],x['sR_laoa'])>25),None)
        print(f"  pull {p:.2f}: alpha pre {sum(x['alpha'] for x in pre)/max(1,len(pre)):5.1f} in roll {a:5.1f} | stab laoa neutral-ish pre {sum((x['sL_laoa']+x['sR_laoa'])/2 for x in pre)/max(1,len(pre)):5.1f} max in roll {lmax:5.1f} | pMax {max(abs(x['p']) for x in R):5.0f}")
