import sys,itertools,statistics,os,math
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from runner import runmany
extra=[a for a in sys.argv[1:] if "=" in a]; verbose="-v" in sys.argv
cases=[]
for ias,p,thr,noise in itertools.product([250,350,450],[0.3,0.45,0.6],[1,0.95],[0,0.004]):
    cases.append(((ias,p,thr,noise),[f"iasKmh={ias}",f"pull={p}","hold=12","trimT=1",f"throttle={thr}","post=0","roll=0","pullRamp=0.3","rideAmp=0.02","rideT=1",f"rideNoise={noise}"]+extra))
res=runmany([a for _,a in cases])
rows=[]
for (k,a),R in zip(cases,res):
    if not R: continue
    W=[x for x in R if -10.5<=x["t"]<=0]
    if not W: continue
    segs={}
    for x in W: segs.setdefault(int(math.floor(x["t"]+10.5)),[]).append(x)
    ovs=[];late=[];t03=[]
    for i,sg in sorted(segs.items()):
        if i<1: continue
        c=sg[-1]["alphaCmd"]; c0=segs.get(i-1,[sg[0]])[-1]["alphaCmd"]
        e=[x["alpha"]-x["alphaCmd"] for x in sg]
        late+=[abs(v) for v,x in zip(e,sg) if x["t"]-sg[0]["t"]>0.5]
        t03.append(next((x['t']-sg[0]['t'] for x in sg if abs(x['alpha']-x['alphaCmd'])<=0.3 and all(abs(y['alpha']-y['alphaCmd'])<=0.3 for y in sg if y['t']>=x['t'])),1.0)) if abs(c-c0)>0.5 else None
        if abs(c-c0)>0.5:
            d=math.copysign(1,c-c0); ovs.append(max(0,max(d*v for v in e)))
    rows.append(dict(t03=statistics.mean(t03) if t03 else 0,t03m=max(t03) if t03 else 0,k=k,ac=statistics.mean(x["alphaCmd"] for x in W),ov=max(ovs) if ovs else 0,ovm=statistics.mean(ovs) if ovs else 0,late=max(late),late95=sorted(late)[int(0.95*len(late))],nz=max(x["nz"] for x in W),bias=statistics.mean(x["alpha"]-x["alphaCmd"] for x in W)))
for noise in [0,0.004]:
  for thr,nm in [(1,"AB"),(0.95,"MIL")]:
    for lo,hi in [(0,25),(25,35),(35,50)]:
        s=[r for r in rows if r["k"][2]==thr and r["k"][3]==noise and lo<=r["ac"]<hi and r["nz"]<9.3 and abs(r["bias"])<3]
        if not s: continue
        print(f"noise {noise:.3f} {nm:3s} AoA {lo:2d}-{hi:2d}: n={len(s)} step overshoot mean {statistics.mean(r['ovm'] for r in s):4.2f} max {max(r['ov'] for r in s):4.2f} | settled |err| 95% {statistics.mean(r['late95'] for r in s):4.2f} max {max(r['late'] for r in s):4.2f} | time to stay within 0.3 mean {statistics.mean(r['t03'] for r in s):4.2f} max {max(r['t03m'] for r in s):4.2f} s")
if verbose:
    for r in rows: print(r["k"], f"cmd {r['ac']:5.1f} ov {r['ov']:4.2f} late95 {r['late95']:4.2f} max {r['late']:4.2f} bias {r['bias']:+5.2f}")
