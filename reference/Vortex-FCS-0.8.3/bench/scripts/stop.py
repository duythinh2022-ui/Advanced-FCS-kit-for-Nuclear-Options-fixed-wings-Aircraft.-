import csv,subprocess,itertools,os,sys,hashlib,math
from concurrent.futures import ThreadPoolExecutor
S=os.path.dirname(os.path.abspath(__file__))
def run(args):
    h=hashlib.md5(" ".join(args).encode()).hexdigest()[:12]; f=f"{S}/ov/s{h}.csv"
    if not os.path.exists(f): subprocess.run(["dotnet","bin/bench.dll","one"]+args+[f"csv={f}"],cwd="/tmp/claude-0/bench",capture_output=True)
    return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(open(f))]
variants={}
for a in sys.argv[1:]:
    if a=="-v": continue
    n,e=a.split(":",1) if ":" in a else (a,"")
    variants[n]=e.split() if e else []
cases=list(itertools.product([250,300,350,450],[0.3,0.45,0.6],[1,0.9]))
jobs=[]
for vn,ve in variants.items():
    for ias,p,thr in cases:
        jobs.append((vn,ias,p,thr,[f"iasKmh={ias}",f"pull={p}","hold=6","pullRamp=0.3","trimT=1",f"throttle={thr}","post=0","rollFromPull=2.5","roll=1","rollT=1.5"]+ve))
with ThreadPoolExecutor(2) as ex: res=list(ex.map(lambda j: run(j[4]),jobs))
agg={}
for j,R in zip(jobs,res):
    tRollEnd=-6+2.5+1.5
    W=[x for x in R if x['t']>=tRollEnd-0.02]
    ps=lambda x: x['p']*math.cos(math.radians(x['alpha']))+x['r']*math.sin(math.radians(x['alpha']))
    p0=ps(W[0]); sgn=1 if p0>=0 else -1
    tstop=next((x['t']-tRollEnd for x in W if sgn*ps(x)<0.2*abs(p0)),9)
    over=min(sgn*ps(x) for x in W[:int(2/0.02)])
    bank=0; prev=None
    for x in W:
        if prev is not None and x['t']-tRollEnd<=tstop: bank+=sgn*ps(x)*(x['t']-prev)
        prev=x['t']
    a=W[0]['alpha']
    agg.setdefault(j[0],[]).append((j[1],j[2],j[3],a,p0*sgn,tstop,bank,over,max(abs(x['beta']) for x in W[:100])))
for vn,L in agg.items():
    import statistics
    print(f"{vn:12s} stop time mean {statistics.mean(l[5] for l in L):.2f}s max {max(l[5] for l in L):.2f} | bank after release mean {statistics.mean(l[6] for l in L):5.1f} | rate at release mean {statistics.mean(l[4] for l in L):5.1f} | reverse overshoot worst {min(l[7] for l in L):5.1f} | beta max {max(l[8] for l in L):4.1f}")
    if "-v" in sys.argv:
        for l in L: print(f"   ias {l[0]} pull {l[1]} thr {l[2]}: alpha {l[3]:4.1f} pS@rel {l[4]:5.1f} stop {l[5]:.2f}s bank {l[6]:5.1f} over {l[7]:5.1f} beta {l[8]:4.1f}")
