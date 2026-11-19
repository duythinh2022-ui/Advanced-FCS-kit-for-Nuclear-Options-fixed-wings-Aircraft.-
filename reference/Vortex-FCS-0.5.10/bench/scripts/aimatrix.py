import csv,subprocess,itertools,statistics,os,sys,hashlib
from concurrent.futures import ThreadPoolExecutor
S=os.path.dirname(os.path.abspath(__file__))
STOCKAF=["FlaperonEffectiveness=1","StabilatorEffectiveness=1","LerxEffectiveness=1","ActuatorScale=1","CgShiftAft=0"]
CFG={"S0 stock FBW, stock airframe":["stock=1"]+STOCKAF,"S1 stock FBW, modded airframe":["stock=1"],"F  FCS AI rate, modded":["stock=0"]}
extra=[a for a in sys.argv[1:] if "=" in a]
def run(args):
    h=hashlib.md5(" ".join(args).encode()).hexdigest()[:12]; f=f"{S}/ai/{h}.csv"
    if not os.path.exists(f): subprocess.run(["dotnet","bin/bench.dll","ai"]+args+[f"csv={f}"],cwd="/tmp/claude-0/bench",capture_output=True)
    try: return [{k:float(v) for k,v in r.items()} for r in csv.DictReader(open(f))]
    except Exception: return []
def metrics(R,T):
    if not R or R[-1]['t']<T-0.1: return None
    t5=next((r['t'] for r in R if r['err']<5),99)
    settle=99
    for i in range(len(R)):
        if all(x['err']<5 for x in R[i:]): settle=R[i]['t']; break
    after=[r for r in R if r['t']>=t5] if t5<99 else []
    rev=0; last=0
    for r in after:
        s=1 if r['sr']>0.3 else (-1 if r['sr']<-0.3 else 0)
        if s and last and s!=last: rev+=1
        if s: last=s
    tail=[r for r in R if r['t']>T-5]
    prms=(sum(r['p']**2 for r in tail)/len(tail))**0.5
    return dict(t5=t5,settle=settle,amax=max(r['alpha'] for r in R),nzmax=max(r['nz'] for r in R),iasmin=min(r['ias'] for r in R),rev=rev,prms=prms,errend=R[-1]['err'],dalt=max(abs(r['alt']-R[0]['alt']) for r in R))
cases=[]
for ias,(hd,el),thr in itertools.product([350,550,800],[(90,0),(180,0),(0,30),(60,-20)],[1,0.9]):
    for cn,ca in CFG.items():
        cases.append((cn,ias,hd,el,thr,ca+[f"ias={ias}",f"hdg={hd}",f"elev={el}",f"thr={thr}","T=25"]+extra))
with ThreadPoolExecutor(2) as ex: res=list(ex.map(lambda c: run(c[5]),cases))
agg={}
rows=[]
for c,R in zip(cases,res):
    m=metrics(R,25)
    rows.append((c,m)); agg.setdefault(c[0],[]).append(m)
print("per-config summary over 24 manoeuvres (350/550/800 km/h x 90/180 turn, 30 up, 60 right+20 down x AB/0.9):")
for cn,M in agg.items():
    ok=[m for m in M if m]; nf=len(M)-len(ok)
    st=[m['settle'] for m in ok]; 
    print(f"  {cn:30s} failed/crashed {nf} | settled<5deg {sum(1 for s in st if s<99)}/{len(ok)} median settle {statistics.median(st):5.1f}s | roll-stick reversals after capture mean {statistics.mean(m['rev'] for m in ok):4.1f} max {max(m['rev'] for m in ok)} | p rms last 5s mean {statistics.mean(m['prms'] for m in ok):5.1f} | AoA max {max(m['amax'] for m in ok):5.1f} (mean peak {statistics.mean(m['amax'] for m in ok):4.1f}) | nz max {max(m['nzmax'] for m in ok):4.1f} | min IAS {min(m['iasmin'] for m in ok):4.0f}")
if "-v" in sys.argv:
    for c,m in rows:
        if m: print(f"{c[0][:2]} ias{c[1]} hdg{c[2]} el{c[3]} thr{c[4]}: t5 {m['t5']:5.1f} settle {m['settle']:5.1f} rev {m['rev']:2d} prms {m['prms']:5.1f} amax {m['amax']:5.1f} nz {m['nzmax']:4.1f} iasmin {m['iasmin']:4.0f} errEnd {m['errend']:4.1f}")
        else: print(f"{c[0][:2]} ias{c[1]} hdg{c[2]} el{c[3]} thr{c[4]}: FAILED")
