#!/usr/bin/env python3
"""Game-like stuck-pose repro: full aft for tp s, release, full right roll held 5 s (TVC off by default).
usage: grel.py tag [Name=value ...]"""
import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *  # kit 5.0: paths relative to this tree
import sys,os,subprocess,json,numpy as np,pandas as pd
from concurrent.futures import ThreadPoolExecutor
B=BENCH; tag=sys.argv[1]; ex=sys.argv[2:]
OUT=f'{RUNS}/{tag}'; os.makedirs(OUT,exist_ok=True)
env=dict(os.environ)
def case(ias,tp,thr,cap=35):
    name=f'g{ias}_{tp}_{thr}'; csv=f'{OUT}/{name}.csv'
    t1=tp+0.05
    a=['dotnet','bin/bench.dll','one',f'iasKmh={ias}','trimT=3','throttle=-1',f'thr2={thr}','thr2T=0',f'prof=0:1,{tp}:1,{t1}:0,8:0',f'rprof=0:0,{tp}:0,{t1}:1,{tp+5}:1,{tp+5.05}:0',f'hold={tp+5}','post=2','DisableTVC=true',f'AoAPos={cap}',f'NoTvcAoACapPos={cap}',f'NoTvcThrCapPos={cap}',f'csv={csv}']+ex
    r=subprocess.run(a,cwd=B,env=env,capture_output=True,text=True)
    if r.returncode: print(name,r.stderr[-300:])
    return name,tp,csv
jobs=[(ias,tp,thr) for ias in (380,470,540) for tp in (0.7,1.2) for thr in (1,0.6)]
with ThreadPoolExecutor(2) as p: res=list(p.map(lambda j:case(*j),jobs))
out={}
for name,tp,csv in res:
    d=pd.read_csv(csv); d['tp']=d.t+tp+5; r=d[(d.tp>=tp+0.8)&(d.tp<tp+5)]
    split=d.stabL-d.stabR
    rev=((np.sign(split)==np.sign(d.sr))&(split.abs()>6)&(d.sr.abs()>0.5)&(d.alpha.abs()<20)).sum()
    out[name]=dict(rate=r.pS.mean() if 'pS' in r else r.p.mean(),pmin=r.p.min(),rev=int(rev),slip=r.beta.abs().max(),amin=d[d.tp>=tp].alpha.min(),unm=r.unmetX.abs().mean() if 'unmetX' in r else np.nan)
json.dump(out,open(f'{OUT}/summary.json','w'),indent=1)
print(tag,' '.join(ex))
for k,v in out.items(): print(f' {k:14s} rate {v["rate"]:6.1f} pmin {v["pmin"]:6.1f} rev {v["rev"]:4d} slip {v["slip"]:5.1f} amin {v["amin"]:6.1f}')
