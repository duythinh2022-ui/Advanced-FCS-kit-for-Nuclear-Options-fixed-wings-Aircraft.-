import sys
sys.path.insert(0,'/home/claude/w/fit')
from lib import runmany
cfgs={'2.0.7':["StabDownwash=0","BodyLiftShaping=false"],'2.0.8':[],'2.0.8b':['BodyLiftFore=0.9','BodyLiftAft=1.8']}
pulls=[0.07,0.14,0.21,0.28,0.35,0.42,0.5]
for nm,c in cfgs.items():
    res=runmany([["iasKmh=450",f"pull={p}","hold=2.5","trimT=1","throttle=1","post=0","roll=0","pullRamp=0.3"]+c for p in pulls])
    out=[]
    for p,D in zip(pulls,res):
        W=[x for x in D if -0.6<=x['t']<=0]
        a=sum(x['alpha'] for x in W)/len(W); n=sum(x['nz'] for x in W)/len(W); ias=W[-1]['ias']
        out.append(f"a{a:4.1f}:nz{n:4.2f}@{ias:3.0f}")
    print(nm,' '.join(out))
