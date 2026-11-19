# reversed-flight lateral cases (MPO tail slide from a 60 deg climb): python3 revflow_jet.py <tree> <baseTree> <label>; EXTRA="Name=value ..." adds bench settings
import subprocess, sys, os, pandas as pd, numpy as np
# usage: jetrev.py <treeNew> <treeBase> <label>
tn, tb, lab = sys.argv[1:4]
env=dict(os.environ, BENCH_MPO='1')
B="ias=300 alt=4000 thr=0.25 T=34 gamma0=60 pitch=0:0".split()+os.environ.get("EXTRA","").split()
def fly(tree, extra, out):
    subprocess.run(['bash', f'{tree}/bench.sh','fly']+B+extra+[f'fcslog={out}'], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return pd.read_csv(out)
d0=fly(tn, ['roll=0:0','yaw=0:0'], f'/tmp/jr_{lab}_0.csv')
def tAt(a):
    w=d0[d0.alpha.abs()>=a]; return float(w.t.iloc[0]) if len(w) else None
tA, tB = tAt(115), tAt(135)
print(f'{lab}: no input reaches 115 deg at t={tA}, 135 at t={tB}, max |AoA| {d0.alpha.abs().max():.0f}')
def prof(t0,v): return f'0:0,{t0:.2f}:0,{t0+0.05:.2f}:{v},{t0+1.5:.2f}:{v},{t0+1.55:.2f}:0'
cases=[('A','stick R',tA,'roll',1),('B','stick L',tB,'roll',-1),('C','pedal L',tA,'yaw',-1),('F','pedal R',tB,'yaw',1)]
for k,nm,t0,ax,v in cases:
    if t0 is None: continue
    for tag,tree in (('old',tb),('new',tn)):
        ex=[f'{ax}={prof(t0,v)}', ('yaw=0:0' if ax=='roll' else 'roll=0:0')]
        d=fly(tree, ex, f'/tmp/jr_{lab}_{tag}_{k}.csv')
        s=d[(d.t>=t0)&(d.t<=t0+1.5)]; w=d[(d.t>=t0)&(d.t<=t0+4.5)]; e=s.iloc[-1]
        print(f' {k} {nm:7s} {tag}: AoA {s.alpha.iloc[0]:6.1f}->{e.alpha:6.1f} ias {e.ias*3.6:4.0f} km/h | end: pCmd {e.pCmd:6.1f} rCmd {e.rCmd:6.1f} p {e.p:6.1f} r {e.r:6.1f} beta {e.beta:6.1f} | peak|beta| 0-4.5s {w.beta.abs().max():5.1f}, beta at +4.5 s {w.beta.iloc[-1]:6.1f}')
