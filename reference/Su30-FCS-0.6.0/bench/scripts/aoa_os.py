import sys,itertools,statistics
sys.path.insert(0,__import__('os').path.dirname(__file__))
from runner import runmany
extra=sys.argv[1:]
cases=[]
for ias,p,thr,lat in itertools.product([200,300,400,500],[0.3,0.5,0.7,1.0],[1,0.9,-1],[0,1]):
    a=[f"iasKmh={ias}",f"pull={p}","hold=3","trimT=1",f"throttle={thr}","post=0","rollFromPull=0.0" if lat else "roll=0",f"roll={lat}","rollT=3"]+extra
    cases.append(((ias,p,thr,lat),a))
res=runmany([a for _,a in cases])
rows=[]
for (k,a),R in zip(cases,res):
    if not R: continue
    P=[x for x in R if -3<=x["t"]<=0]
    ac=max(x["alphaCmd"] for x in P)
    am=max(x["alpha"] for x in P)
    # overshoot relative to AoA command, only meaningful when AoA-limited (not G): use final 0.5s alpha as settled
    fin=statistics.mean(x["alpha"] for x in P if x["t"]>-0.5)
    nzmax=max(x["nz"] for x in P)
    rows.append((k,ac,am,fin,nzmax))
def summ(sel,label):
    s=[r for r in rows if sel(r)]
    os_=[r[2]-min(r[1],r[3]+0.0) for r in s]
    print(f"{label:28s} n={len(s):3d} overshoot vs settled mean {statistics.mean(r[2]-r[3] for r in s):5.1f} max {max(r[2]-r[3] for r in s):5.1f} | peak-cmd mean {statistics.mean(r[2]-r[1] for r in s):5.1f} max {max(r[2]-r[1] for r in s):5.1f}")
for thr,nm in [(1,"AB"),(0.9,"MIL 0.9"),(-1,"trim thr")]:
    for lat in [0,1]:
        summ(lambda r:r[0][2]==thr and r[0][3]==lat and r[4]<9.0, f"{nm} lat={lat}")
print("per case (ias pull thr lat): alphaCmd peak settled nzmax")
for r in rows:
    if r[2]-r[3]>3: print(r[0], f"{r[1]:.1f} {r[2]:.1f} {r[3]:.1f} {r[4]:.1f}")
