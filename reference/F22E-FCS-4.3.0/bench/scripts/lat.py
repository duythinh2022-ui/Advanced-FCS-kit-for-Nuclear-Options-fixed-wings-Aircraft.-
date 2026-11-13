import sys,itertools,statistics,math,os
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from runner import runmany
extra=[a for a in sys.argv[1:] if "=" in a]
verbose="-v" in sys.argv
def metrics(R,t0,t1,side):
    W=[x for x in R if t0<=x["t"]<=t1]
    if len(W)<10: return None
    pw=[side*(x["p"]*math.cos(math.radians(x["alpha"]))+x["r"]*math.sin(math.radians(x["alpha"]))) for x in W]
    pk=max(pw); on=next(i for i,v in enumerate(pw) if v>=0.6*pk) if pk>5 else len(pw)
    run=-1e9; dip=0
    for v in pw[on:]:
        run=max(run,v); dip=max(dip,run-v)
    d2=[pw[i+1]-2*pw[i]+pw[i-1] for i in range(max(1,on),len(pw)-1)]
    jerk=math.sqrt(sum(x*x for x in d2)/max(1,len(d2)))/0.02**2/1000  # k deg/s^3
    mean=statistics.mean(pw[on:]) if on<len(pw) else 0
    bmax=max(abs(x["beta"]) for x in W); amax=max(x["alpha"] for x in W)
    t10=next((W[i]["t"]-t0 for i,v in enumerate(pw) if v>=10),9.0)
    end=statistics.mean(pw[-10:])
    return dict(pk=pk,dip=dip,jerk=jerk,mean=mean,bmax=bmax,amax=amax,t10=t10,end=end)
cases=[]
# A: back-flip from vertical climb
for ias,rf in itertools.product([250,350,450],[0.5,0.8,1.2]):
    hold=rf+3.0
    cases.append(("A backflip",f"{ias} rf{rf}",[f"gamma0=85",f"iasKmh={ias}","trimT=0.6","pull=1",f"hold={hold}",f"rollFromPull={rf}","roll=1","rollT=3","throttle=1","post=0"],(rf-hold+0.0,0.0,1)))
# B: moderate AoA held + roll
for ias,p,thr in itertools.product([250,350,450],[0.35,0.45,0.55],[1,0.9]):
    cases.append(("B modAoA roll",f"{ias} p{p} thr{thr}",[f"iasKmh={ias}","trimT=1",f"pull={p}","hold=4","rollFromPull=1.5","roll=1","rollT=2.5",f"throttle={thr}","post=0"],(-2.5,0.0,1)))
# C: falling leaf: full aft, roll left, then release pitch and reverse roll right
for ias,delay in itertools.product([200,250,300],[0.0,0.3]):
    hold=3.0
    cases.append(("C leaf release+rev",f"{ias} d{delay}",[f"iasKmh={ias}","trimT=1","pull=1",f"hold={hold}","rollFromPull=1.0","roll=-1",f"rollRev={hold-1.0+delay}","rollT=5","throttle=1","post=3"],(delay,delay+2.5,1)))
    cases.append(("C' leaf hold+rev",f"{ias} d{delay}",[f"iasKmh={ias}","trimT=1","pull=1","hold=8","rollFromPull=1.0","roll=-1",f"rollRev={2.0+delay}","rollT=5","throttle=1","post=0"],(-8+1+2+delay,-8+1+2+delay+2.5,1)))
# D: post-stall loop from level with lateral held (over the top into leaf)
for ias in [300,400,500]:
    cases.append(("D loop+lat",f"{ias}",[f"iasKmh={ias}","trimT=1","pull=1","hold=7","rollFromPull=0.3","roll=1","rollT=6.7","throttle=1","post=0"],(-6.4,0.0,1)))
for ias,p,thr in itertools.product([250,350,450],[0.5,0.75,1.0],[1,0.95]):
    cases.append(("E pull+roll",f"{ias} p{p} thr{thr}",[f"iasKmh={ias}","trimT=1",f"pull={p}","hold=3","rollFromPull=0.0","roll=1","rollT=3",f"throttle={thr}","post=0"],(-3.0,0.0,1)))
res=runmany([c[2]+extra for c in cases])
groups={}
for c,R in zip(cases,res):
    m=metrics(R,*c[3]) if R else None
    if m is None: continue
    groups.setdefault(c[0],[]).append((c[1],m))
    if verbose: print(f"{c[0]:20s} {c[1]:18s} pk {m['pk']:5.1f} t10 {m['t10']:4.2f} end {m['end']:5.1f} mean {m['mean']:5.1f} dip {m['dip']:5.1f} jerk {m['jerk']:5.1f} |b| {m['bmax']:4.1f} amax {m['amax']:4.0f}")
for g,L in groups.items():
    M=[m for _,m in L]
    if g.startswith("C"):
        print(f"{g:20s} n={len(M):2d} time to +10 deg/s after reversal mean {statistics.mean(m['t10'] for m in M):4.2f} max {max(m['t10'] for m in M):4.2f} | rate at end mean {statistics.mean(m['end'] for m in M):5.1f} | |beta|max {max(m['bmax'] for m in M):4.1f}")
        continue
    print(f"{g:20s} n={len(M):2d} pk {statistics.mean(m['pk'] for m in M):5.1f} mean-rate {statistics.mean(m['mean'] for m in M):5.1f} | dip mean {statistics.mean(m['dip'] for m in M):5.1f} max {max(m['dip'] for m in M):5.1f} | jerk {statistics.mean(m['jerk'] for m in M):5.2f} | |beta|max {max(m['bmax'] for m in M):4.1f}")
