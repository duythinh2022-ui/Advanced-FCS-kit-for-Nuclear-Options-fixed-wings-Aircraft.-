import sys,csv,subprocess,hashlib,os,statistics as st,itertools
from concurrent.futures import ThreadPoolExecutor
B='/home/claude/w/src/bench'; R='/home/claude/w/fit/runs'
def run1(args):
    h=hashlib.md5(" ".join(args).encode()).hexdigest()[:14]; f=f"{R}/{h}.csv"
    subprocess.run(["dotnet","bin/bench.dll","one"]+args+[f"csv={f}"],cwd=B,capture_output=True)
    try: return [{k:float(v) for k,v in x.items()} for x in csv.DictReader(open(f))]
    except Exception as e: return []
def runmany(L,par=4):
    with ThreadPoolExecutor(par) as ex: return list(ex.map(run1,L))
cfg=[a for a in sys.argv[1:] if '=' in a]
label=' '.join(a for a in sys.argv[1:] if '=' not in a) or 'cfg'
ias_list=[300,450]; thr_list=[1,0.95,0.3]; pulls=[0.07,0.14,0.21,0.28,0.35,0.42,0.5,1.0]
cases=[(i,t,p) for i in ias_list for t in thr_list for p in pulls]
res=runmany([[f"iasKmh={i}",f"pull={p}","hold=6","trimT=1",f"throttle={t}","post=0","roll=0","pullRamp=0.4"]+cfg for i,t,p in cases])
print(f"== {label}: {' '.join(cfg)}")
print(" ias thr  pull |  alpha  sd  | stab(mean L/R)  tvc   flapL ailL rud | ias_end")
for (i,t,p),D in zip(cases,res):
    W=[x for x in D if -2.0<=x['t']<=0]
    if not W: print(i,t,p,'no data'); continue
    m=lambda k: st.mean(x[k] for x in W)
    a=m('alpha'); sd=st.pstdev([x['alpha'] for x in W])
    print(f"{i:4d} {t:4.2f} {p:5.2f} | {a:6.1f} {sd:4.2f} | {m('stabL'):6.1f} {m('stabR'):6.1f} {m('tvc'):6.1f} {m('flapL'):6.1f} {m('ailL'):5.1f} {m('rudL'):5.1f} | {W[-1]['ias']:5.0f}")
