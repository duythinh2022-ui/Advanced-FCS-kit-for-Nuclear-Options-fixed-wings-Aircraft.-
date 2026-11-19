import sys,itertools,statistics,math,os
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from runner import runmany
extra=[a for a in sys.argv[1:] if "=" in a]
cases=[]
for ias,p,thr in itertools.product([250,350,450],[0.35,0.45,0.55],[1,0.9]):
    cases.append((ias,p,thr,[f"iasKmh={ias}","trimT=1",f"pull={p}","hold=5","rollFromPull=1.5","roll=1","rollT=3","rollRev=1.2",f"throttle={thr}","post=0"]))
res=runmany([c[3]+extra for c in cases])
tt=[];st=[]
for c,R in zip(cases,res):
    tRev=1.5+1.2-5
    W=[x for x in R if x["t"]>=tRev]
    pw=[x["p"]*math.cos(math.radians(x["alpha"]))+x["r"]*math.sin(math.radians(x["alpha"])) for x in W]
    t20=next((W[i]["t"]-tRev for i,v in enumerate(pw) if v<=-20),9)
    tt.append(t20)
for c,t in zip(cases,tt): print(" ",c[:3],round(t,2))
print(f"reversal at moderate AoA n={len(tt)}: time from reversal to -20 deg/s mean {statistics.mean(tt):.2f} max {max(tt):.2f}")
