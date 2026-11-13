#!/usr/bin/env python3
# Compass max-usable-AoA sweep: held pulls, loaded rolls at the limit, push + roll, idle pulls. Scores from the FCS log.
import subprocess, sys, os, itertools, concurrent.futures as cf
import pandas as pd, numpy as np
R=os.path.dirname(os.path.dirname(os.path.abspath(__file__))); BIN=os.path.join(R,'bin','bench.dll'); OUT=os.path.join(R,'out','csw'); os.makedirs(OUT,exist_ok=True)
extra=sys.argv[2:]; tag=sys.argv[1]
def run(name,args):
    log=os.path.join(OUT,f'{tag}_{name}.csv')
    subprocess.run(['dotnet',BIN,'ifly','alt=3000',f'fcslog={log}']+args+extra,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    try: d=pd.read_csv(log)
    except Exception as e: return name,None
    return name,d
cases=[]
for ias in (200,300,450,600):
    cases.append((f'pull{ias}',[f'ias={ias}','thr=1','T=6','pitch=0:0,0.3:1']))
for ias in (250,350,450):
    cases.append((f'lroll{ias}',[f'ias={ias}','thr=1','T=8','pitch=0:0,0.3:1','roll=0:0,2.5:0,2.6:1,5.5:1,5.6:-1,7.0:-1,7.1:0']))
for ias in (250,400):
    cases.append((f'push{ias}',[f'ias={ias}','thr=1','T=7','pitch=0:0,0.3:-1','roll=0:0,2.5:0,2.6:1,5.0:1,5.1:0']))
for ias in (250,350):
    cases.append((f'idle{ias}',[f'ias={ias}','thr=0','T=6','pitch=0:0,0.3:1']))
cases.append(('rel350',['ias=350','thr=1','T=8','pitch=0:0,0.3:1,4:1,4.1:0','roll=0:0,2:0,2.1:1,4:1,4.1:0']))
with cf.ThreadPoolExecutor(2) as ex: res=dict(ex.map(lambda c: run(*c),cases))
print(f'== {tag} {" ".join(extra)}')
print(f'{"case":9} {"aMax":>6} {"aMin":>6} {"aHold":>6} {"aSd":>5} {"|b|max":>6} {"pVmax":>6} {"qMax":>5} {"elMax":>5} {"ailMax":>6} {"rudMax":>6} {"iasEnd":>6}')
for n,_ in cases:
    d=res[n]
    if d is None: print(n,'FAILED'); continue
    a=d.alpha; b=d.beta
    hold=d[(d.t>2.0)&(d.t<2.5)].alpha if n.startswith('lroll') or n.startswith('push') else d[d.t>3.5].alpha
    ar=np.radians(d.alpha); pS=(d.p*np.cos(ar)+d.r*np.sin(ar)); pv=pS.abs().max()
    el=max(d.elevator_L_now.abs().max(),d.elevator_R_now.abs().max()); ail=max(d.aileron_L_now.abs().max(),d.aileron_R_now.abs().max()); rud=max(d.rudder_L_now.abs().max(),d.rudder_R_now.abs().max())
    print(f'{n:9} {a.max():6.1f} {a.min():6.1f} {hold.mean():6.1f} {hold.std():5.2f} {b.abs().max():6.1f} {pv:6.0f} {d.q.abs().max():5.0f} {el:5.1f} {ail:6.1f} {rud:6.1f} {d.ias.iloc[-1]*3.6:6.0f}')
