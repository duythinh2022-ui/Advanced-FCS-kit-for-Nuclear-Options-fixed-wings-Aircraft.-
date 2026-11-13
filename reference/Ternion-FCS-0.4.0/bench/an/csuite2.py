#!/usr/bin/env python3
# Compass low-AoA / lateral checks: 1 g full rolls, reversals, pedal kicks, release-after-pull, engine out.
import subprocess, sys, os, concurrent.futures as cf
import pandas as pd, numpy as np
R=os.path.dirname(os.path.dirname(os.path.abspath(__file__))); BIN=os.path.join(R,'bin','bench.dll'); OUT=os.path.join(R,'out','cs2'); os.makedirs(OUT,exist_ok=True)
tag=sys.argv[1]; extra=sys.argv[2:]
def run(name,args):
    log=os.path.join(OUT,f'{tag}_{name}.csv')
    subprocess.run(['dotnet',BIN,'ifly','alt=3000',f'fcslog={log}']+args+extra,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    try: return name,pd.read_csv(log)
    except Exception: return name,None
C=[]
for ias in (300,500,700): C.append((f'roll{ias}',[f'ias={ias}','thr=1','T=5','roll=0:0,0.5:0,0.55:1,3.5:1,3.55:0']))
for ias in (300,500): C.append((f'rev{ias}',[f'ias={ias}','thr=1','T=6','roll=0:0,0.5:0,0.55:1,1.8:1,1.85:-1,3.5:-1,3.55:0']))
for ias in (250,450): C.append((f'pedal{ias}',[f'ias={ias}','thr=1','T=5','yaw=0:0,0.5:0,0.55:1,2.5:1,2.55:0']))
for ias in (300,500): C.append((f'pullrel{ias}',[f'ias={ias}','thr=1','T=5','pitch=0:0,0.3:0.6,2:0.6,2.05:0']))
C.append(('engout250',['ias=250','thr=1','T=8','engout=1.0','pitch=0:0,1:0.5,8:0.5']))
with cf.ThreadPoolExecutor(2) as ex: res=dict(ex.map(lambda c: run(*c),C))
print(f'== {tag} {" ".join(extra)}')
print(f'{"case":10} {"pMax":>5} {"t90":>5} {"|b|max":>6} {"aMax":>5} {"aMin":>5} {"nzMax":>5} {"phiEnd":>6} {"psiDrift":>8} {"qAfter":>6} {"pAfter":>6}')
for n,_ in C:
    d=res[n]
    if d is None: print(n,'FAILED'); continue
    t90=d[d.phi.abs()>=90].t.min()-0.55 if (d.phi.abs()>=90).any() else np.nan
    tail=d[d.t>d.t.max()-1.0]
    print(f'{n:10} {d.p.abs().max():5.0f} {t90:5.2f} {d.beta.abs().max():6.1f} {d.alpha.max():5.1f} {d.alpha.min():5.1f} {d.nz.max():5.2f} {d.phi.iloc[-1]:6.0f} {d.r.abs().max():8.1f} {tail.q.abs().mean():6.2f} {tail.p.abs().mean():6.2f}')
