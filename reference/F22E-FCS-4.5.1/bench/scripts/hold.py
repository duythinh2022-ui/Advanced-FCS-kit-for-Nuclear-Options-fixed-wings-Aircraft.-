import sys,itertools,statistics,os,math
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from runner import runmany
extra=[a for a in sys.argv[1:] if "=" in a]; verbose="-v" in sys.argv
cases=[]
for ias,p,thr in itertools.product([250,350,450],[0.2,0.3,0.4,0.5,0.6,0.8],[1,0.95]):
    cases.append(((ias,p,thr),[f"iasKmh={ias}",f"pull={p}","hold=5","trimT=1",f"throttle={thr}","post=0","roll=0","pullRamp=0.3"]+extra))
res=runmany([a for _,a in cases])
rows=[]
for (k,a),R in zip(cases,res):
    if not R: continue
    W=[x for x in R if -2.5<=x["t"]<=0]   # last 2.5 s of the 5 s hold
    if not W: continue
    e=[x["alpha"]-x["alphaCmd"] for x in W]
    ac=W[-1]["alphaCmd"]; nz=max(x["nz"] for x in W)
    qe=[x["q"]-x["qT"] for x in W]
    rows.append(dict(k=k,ac=ac,mean=statistics.mean(e),sd=statistics.pstdev(e),mx=max(abs(v) for v in e),nz=nz,qe=math.sqrt(sum(v*v for v in qe)/len(qe)),ias=W[-1]["ias"]))
bands=[(0,15),(15,25),(25,35),(35,50),(50,70)]
for thr,nm in [(1,"AB"),(0.95,"MIL")]:
    for lo,hi in bands:
        s=[r for r in rows if r["k"][2]==thr and lo<=r["ac"]<hi and r["nz"]<9.3 and abs(r["mean"])<5]
        if not s: continue
        print(f"{nm:3s} AoA {lo:2d}-{hi:2d}: n={len(s):2d} bias mean {statistics.mean(r['mean'] for r in s):+5.2f} | sd mean {statistics.mean(r['sd'] for r in s):4.2f} | |err| max {max(r['mx'] for r in s):4.2f} | q-qT rms {statistics.mean(r['qe'] for r in s):4.2f}")
if verbose:
    for r in rows: print(r["k"], f"cmd {r['ac']:5.1f} ias {r['ias']:4.0f} bias {r['mean']:+5.2f} sd {r['sd']:4.2f} max {r['mx']:4.2f} nz {r['nz']:3.1f} qerr {r['qe']:4.2f}")
