#!/usr/bin/env python3
"""Chatter repro: full forward (push) or aft for tp s, release, full right roll held 4 s; raw stab command reversals.
usage: prel.py tag [Name=value ...]"""
import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *  # kit 5.0: paths relative to this tree
import sys,os,subprocess,json,numpy as np,pandas as pd
from concurrent.futures import ThreadPoolExecutor
B=BENCH; tag=sys.argv[1]; ex=sys.argv[2:]
OUT=f'{RUNS}/{tag}'; os.makedirs(OUT,exist_ok=True)
env=dict(os.environ)
def case(ias,tp,sgn,thr):
    name=f'p{ias}_{tp}_{"push" if sgn<0 else "pull"}_{thr}'; csv=f'{OUT}/{name}.csv'
    t1=tp+0.05
    a=['dotnet','bin/bench.dll','one',f'iasKmh={ias}','trimT=3','throttle=-1',f'thr2={thr}','thr2T=0',f'prof=0:{sgn},{tp}:{sgn},{t1}:0,8:0',f'rprof=0:0,{tp}:0,{t1}:1,{tp+4}:1,{tp+4.05}:0',f'hold={tp+4}','post=1.5','DisableTVC=true',f'csv={csv}']+ex
    r=subprocess.run(a,cwd=B,env=env,capture_output=True,text=True)
    if r.returncode: print(name,r.stderr[-300:])
    return name,tp,csv
jobs=[(ias,tp,sgn,thr) for ias in (300,380,470) for tp in (0.8,1.5) for sgn in (-1,1) for thr in (1,0.5)]
with ThreadPoolExecutor(2) as p: res=list(p.map(lambda j:case(*j),jobs))
out={}
def rv(v):
    dv=np.diff(v); return int(((dv[1:]*dv[:-1]<0)&(np.abs(dv[1:])>3)&(np.abs(dv[:-1])>3)).sum())
for name,tp,csv in res:
    d=pd.read_csv(csv); d['tp']=d.t+tp+4; r=d[(d.tp>=tp)&(d.tp<tp+4)]
    t90=r[r.p>=0.9*r.p.max()].tp.min()-tp if len(r) else np.nan
    out[name]=dict(rev=rv(r.sLraw.values)+rv(r.sRraw.values),swing=float(np.abs(np.diff(r.stabL.values)).sum()+np.abs(np.diff(r.stabR.values)).sum()),rate=float(r.p.mean()),t90=float(t90),slip=float(r.beta.abs().max()))
json.dump(out,open(f'{OUT}/summary.json','w'),indent=1)
tot=sum(v['rev'] for v in out.values()); sw=sum(v['swing'] for v in out.values())
print(tag,' '.join(ex),f'TOTAL raw reversals {tot} stab travel {sw:.0f} deg')
for k,v in out.items(): print(f' {k:22s} rev {v["rev"]:3d} travel {v["swing"]:6.0f} rate {v["rate"]:6.1f} t90 {v["t90"]:4.2f} slip {v["slip"]:4.1f}')
