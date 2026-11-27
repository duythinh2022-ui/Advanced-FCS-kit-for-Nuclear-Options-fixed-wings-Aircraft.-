import csv,subprocess,itertools,os,sys,hashlib
from concurrent.futures import ThreadPoolExecutor
S=os.path.dirname(os.path.abspath(__file__))
def run(args):
    h=hashlib.md5(" ".join(args).encode()).hexdigest()[:12]; f=f"{S}/ov/{h}.csv"
    if not os.path.exists(f): subprocess.run(["dotnet","bin/bench.dll","one"]+args+[f"csv={f}"],cwd="/tmp/claude-0/bench",capture_output=True)
    return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(open(f))]
variants={}
for a in sys.argv[1:]:
    n,e=a.split(":",1) if ":" in a else (a,"")
    variants[n]=e.split() if e else []
cases=list(itertools.product([400,475,550],[1,0.9,0.75],[1,0.5]))
jobs=[]
for vn,ve in variants.items():
    for ias,thr,roll in cases:
        jobs.append((vn,ias,thr,roll,[f"iasKmh={ias}","pull=1","hold=4","trimT=1",f"throttle={thr}","post=0","rollFromPull=0",f"roll={roll}","rollT=4"]+ve))
with ThreadPoolExecutor(2) as ex: res=list(ex.map(lambda j: run(j[4]),jobs))
print("variant | per case peak AoA (ias/thr/roll) ... | worst peak | mean peak | mean AoA last 1s | mean rise t(60deg)")
agg={}
for j,R in zip(jobs,res):
    W=[x for x in R if x['t']>=-4]
    pk=max(x['alpha'] for x in W); fin=sum(x['alpha'] for x in W if x['t']>-1)/max(1,len([x for x in W if x['t']>-1]))
    t60=next((x['t']+4 for x in W if x['alpha']>=60),9)
    agg.setdefault(j[0],[]).append((j[1],j[2],j[3],pk,fin,t60))
for vn,L in agg.items():
    print(f"{vn:10s} worst {max(l[3] for l in L):5.1f} mean {sum(l[3] for l in L)/len(L):5.1f} | settled mean {sum(l[4] for l in L)/len(L):5.1f} | t60 mean {sum(l[5] for l in L)/len(L):4.2f}")
    print("   "+"  ".join(f"{l[0]}/{l[1]}/{l[2]}:{l[3]:.1f}" for l in L))
