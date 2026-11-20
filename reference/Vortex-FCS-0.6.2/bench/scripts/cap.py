import sys,itertools,statistics,os
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from runner import runmany
extra=[a for a in sys.argv[1:] if "=" in a]
cases=[]
for ias,p,thr,lat in itertools.product([200,275,350,425,500],[0.4,0.55,0.7,0.85,1.0],[1,0.95],[0,1]):
    a=[f"iasKmh={ias}",f"pull={p}","hold=3.5","trimT=1",f"throttle={thr}","post=0",f"roll={lat}","rollFromPull=0.0","rollT=3.5"]+extra
    cases.append(((ias,p,thr,lat),a))
res=runmany([a for _,a in cases])
rows=[]
for (k,a),R in zip(cases,res):
    if not R: continue
    P=[x for x in R if -3.5<=x["t"]<=0]
    ac=max(x["alphaCmd"] for x in P); am=max(x["alpha"] for x in P)
    fin=statistics.mean(x["alpha"] for x in P if x["t"]>-0.5)
    nzm=max(x["nz"] for x in P)
    # time to reach within 2 deg of peak command region (rise time to 90% of cmd)
    t90=next((x["t"]+3.5 for x in P if x["alpha"]>=0.9*ac),9)
    rows.append(dict(k=k,ac=ac,am=am,fin=fin,nz=nzm,t90=t90))
for thr,nm in [(1,"AB"),(0.95,"MIL")]:
    for lat in [0,1]:
        s=[r for r in rows if r["k"][2]==thr and r["k"][3]==lat and r["nz"]<9.3 and abs(r["fin"]-r["ac"])<3]
        u=[r for r in rows if r["k"][2]==thr and r["k"][3]==lat and r["nz"]<9.3 and abs(r["fin"]-r["ac"])>=3]
        print(f"{nm:4s} lat={lat}: holdable n={len(s):2d} overshoot (peak-cmd) mean {statistics.mean(r['am']-r['ac'] for r in s):4.1f} max {max(r['am']-r['ac'] for r in s):5.1f} | rise t90 mean {statistics.mean(r['t90'] for r in s):4.2f} | not holdable n={len(u)}")

if "-v" in sys.argv:  # VERB
    for r in rows:
        if r["k"][3]==0 and r["nz"]<9.3 and abs(r["fin"]-r["ac"])<3 and r["am"]-r["ac"]>1.5: print(r["k"], f"cmd {r['ac']:.1f} peak {r['am']:.1f} os {r['am']-r['ac']:.1f} t90 {r['t90']:.2f}")
