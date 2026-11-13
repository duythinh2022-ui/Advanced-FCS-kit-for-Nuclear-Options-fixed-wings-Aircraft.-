import sys
sys.path.insert(0,'/home/claude/w/fit')
from lib import runmany
cfgs=[c.split(',') if c else [] for c in sys.argv[1:]]
for c in cfgs:
    res=runmany([[f"iasKmh={i}","pull=1","hold=6","rollFromPull=2.5","roll=1","yaw=1","rollT=3","trimT=1","throttle=1","post=0.2"]+c for i in (200,300)])
    out=[]
    for i,D in zip((200,300),res):
        W=[x for x in D if x['sr']>0.5 or abs(x['r'])>0]
        rm=max(abs(x['r']) for x in D); bm=max(abs(x['beta']) for x in D)
        R=[x for x in D if abs(x['sr'])>0.5]
        sm=sum(abs(x['stabL']-x['stabR']) for x in R)/max(1,len(R)); st=sum((x['stabL']+x['stabR'])/2 for x in R)/max(1,len(R)); tv=sum(x['tvc'] for x in R)/max(1,len(R))
        out.append(f"{i}: rMax {rm:5.1f} |b|max {bm:4.1f} stabSym {st:5.1f} stabDiff {sm:5.1f} tvc {tv:5.1f}")
    print(f"{' '.join(c) or 'default':60s} | "+" | ".join(out))
