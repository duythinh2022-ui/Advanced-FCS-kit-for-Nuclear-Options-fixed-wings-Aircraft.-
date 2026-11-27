#!/usr/bin/env python3
"""Battery rebuilt from the 25 Nov docs (val.py was not available).
rev: 450 km/h, full thrust, no TVC; full aft at 0; full roll right 2-4 s; full left 4-6.5 s; release both at 6.5.
slow: 320 km/h idle; full aft at 0; full roll right 2-5 s; release at 5.
push: 450 km/h full thrust; full aft at 0; full roll right from 2 s held; full forward at 3.5 s.
pull: 450 km/h full thrust; full roll right 0-1.5 s at 1 g, full aft from 0.3 s, release 3 s.
usage: bat.py tag [Name=value ...]   (env BIN=bench dir)"""
import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *  # kit 5.0: paths relative to this tree
import sys,os,subprocess,json,numpy as np,pandas as pd
from concurrent.futures import ThreadPoolExecutor
B=os.environ.get('BIN',BENCH); tag=sys.argv[1]; ex=sys.argv[2:]
OUT=f'{RUNS}/{tag}'; os.makedirs(OUT,exist_ok=True)
env=dict(os.environ)
def case(name,cap,ias,thr,prof,rprof,hold,post=3):
    csv=f'{OUT}/{name}{cap}.csv'
    a=['dotnet','bin/bench.dll','one',f'iasKmh={ias}','trimT=3','throttle=-1',f'thr2={thr}','thr2T=0',f'prof={prof}',f'rprof={rprof}',f'hold={hold}',f'post={post}','DisableTVC=true',f'AoAPos={cap}',f'NoTvcAoACapPos={cap}',f'NoTvcThrCapPos={cap}',f'csv={csv}']+ex
    r=subprocess.run(a,cwd=B,env=env,capture_output=True,text=True)
    if r.returncode: print(name,cap,r.stderr[-400:])
    return name,cap,csv
jobs=[('rev',c,450,1,'0:1,6.5:1,6.51:0','0:0,2:0,2.01:1,4:1,4.01:-1,6.5:-1,6.51:0',6.5) for c in (30,36,45,50)]
jobs+=[('slow',c,320,0,'0:1,5:1,5.01:0','0:0,2:0,2.01:1,5:1,5.01:0',5) for c in (36,45)]
jobs+=[('push',c,450,1,'0:1,3.5:1,3.51:-1,6:-1','0:0,2:0,2.01:1,6:1',6) for c in (36,45)]
jobs+=[('proll',c,450,1,'0:1,2:1,2.01:0,5.3:0','0:0,2.3:0,2.31:1,5.3:1,5.31:0',5.3) for c in (36,45)]
jobs+=[('prolls',c,320,0,'0:1,2:1,2.01:0,5.3:0','0:0,2.3:0,2.31:1,5.3:1,5.31:0',5.3) for c in (36,45)]
jobs+=[('half',c,320,0,'0:1,2:1,2.01:0.5,5.3:0.5,5.31:0','0:0,2.3:0,2.31:1,5.3:1,5.31:0',5.3) for c in (36,45)]
jobs+=[('pull',c,450,1,'0:0,0.3:0,0.31:1,3:1,3.01:0','0:1,1.5:1,1.51:0',3) for c in (36,45)]
with ThreadPoolExecutor(int(os.environ.get('J','2'))) as p: res=list(p.map(lambda j:case(*j),jobs))
def ld(csv,hold):
    d=pd.read_csv(csv); d=d.copy(); d['tp']=d.t+hold; return d
out={}
for name,cap,csv in res:
    if name=='rev':
        d=ld(csv,6.5); w=d[(d.tp>=3.0)&(d.tp<4.0)]; rate=w.pS.mean()
        man=d[(d.tp>=2.0)&(d.tp<9.5)]; ov=man.alpha.max()-cap; sl=man.beta.abs().max()
        aft=d[d.tp>=6.5]; st=aft[aft.pS.abs()<2.0]; tstop=(st.tp.iloc[0]-6.5) if len(st) else np.nan
        coast=float(np.trapezoid(aft.pS.abs(),aft.tp)) if len(aft) else np.nan
        on=d[(d.tp>=2.0)&(d.tp<2.6)]; trim=d[(d.tp>=1.6)&(d.tp<2.0)]
        sLt,sRt=trim.stabL.mean(),trim.stabR.mean()
        i=(on.stabR-sRt).abs().idxmax(); j=(on.stabL-sLt).abs().idxmax()
        out[f'rev{cap}']=dict(rate=rate,ov=ov,slip=sl,stop=tstop,coast=coast,trimL=sLt,trimR=sRt,onOut=on.stabL[j]-sLt,onIn=on.stabR[i]-sRt,
            aMaxOn=d[(d.tp>=2)&(d.tp<2.45)].alpha.max()-cap)
    elif name=='slow':
        d=ld(csv,5); man=d[(d.tp>=2.0)&(d.tp<8)]
        out[f'slow{cap}']=dict(ov=man.alpha.max()-cap,rate=d[(d.tp>=3)&(d.tp<5)].pS.mean(),slip=man.beta.abs().max(),ias=d[(d.tp>=2)&(d.tp<5)].ias.mean())
    elif name=='push':
        d=ld(csv,6); q0=d.iloc[(d.tp-3.5).abs().argmin()].q; q1=d.iloc[(d.tp-4.5).abs().argmin()].q
        out[f'push{cap}']=dict(dq=q1-q0,slip=d[d.tp>=2].beta.abs().max())
    elif name in ('proll','prolls','half'):
        d=ld(csv,5.3); man=d[(d.tp>=2.3)&(d.tp<8)]; a0=d.iloc[(d.tp-2.3).abs().argmin()].alpha
        out[f'{name}{cap}']=dict(ov=man.alpha.max()-cap,a0=a0,rate=d[(d.tp>=3.3)&(d.tp<5.3)].pS.mean(),slip=man.beta.abs().max(),amin=man.alpha.min())
    else:
        d=ld(csv,3); man=d[d.tp>=0.3]
        out[f'pull{cap}']=dict(ov=man.alpha.max()-cap,slip=man.beta.abs().max())
json.dump(out,open(f'{OUT}/summary.json','w'),indent=1)
f=lambda x:f'{x:6.1f}'
print(tag,' '.join(ex))
print(' rev rate 30/36/45/50 :',' / '.join(f(out[f'rev{c}']['rate']) for c in (30,36,45,50)))
print(' rev ov               :',' / '.join(f(out[f'rev{c}']['ov']) for c in (30,36,45,50)))
print(' rev slip             :',' / '.join(f(out[f'rev{c}']['slip']) for c in (30,36,45,50)))
print(' rev stop s           :',' / '.join(f'{out[f"rev{c}"]["stop"]:6.2f}' for c in (30,36,45,50)))
print(' rev coast deg        :',' / '.join(f(out[f'rev{c}']['coast']) for c in (30,36,45,50)))
print(' trim L/R             :',' / '.join(f"{out[f'rev{c}']['trimL']:+.1f}" for c in (30,36,45,50)))
print(' onset out/in         :',' / '.join(f"{out[f'rev{c}']['onOut']:+.1f}/{out[f'rev{c}']['onIn']:+.1f}" for c in (30,36,45,50)))
print(' slow ov 36/45        :',' / '.join(f(out[f'slow{c}']['ov']) for c in (36,45)),' rate',' / '.join(f(out[f'slow{c}']['rate']) for c in (36,45)),' slip',' / '.join(f(out[f'slow{c}']['slip']) for c in (36,45)))
print(' push dq 1s 36/45     :',' / '.join(f(out[f'push{c}']['dq']) for c in (36,45)))
print(' pull ov/slip 36/45   :',' / '.join(f"{out[f'pull{c}']['ov']:+.1f}/{out[f'pull{c}']['slip']:.1f}" for c in (36,45)))
for n in ('proll','prolls','half'):
    print(f' {n:6s} a0/ov/rate/slip 36,45:',' | '.join(f"{out[f'{n}{c}']['a0']:.1f}/{out[f'{n}{c}']['ov']:+.1f}/{out[f'{n}{c}']['rate']:.1f}/{out[f'{n}{c}']['slip']:.1f}" for c in (36,45)))
