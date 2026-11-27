# Perturbation ensemble for the reversal cases: each case is flown over a small grid of entry speeds and reversal times,
# so a regression is judged on the spread (median / worst) instead of one run that may sit on either side of a cliff.
# usage: BENCH_DATA=... REG_S=... python3 ens.py <bench dir> <tag> [sets...]
import sys, os, subprocess, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
B = sys.argv[1]; tag = sys.argv[2]; sets = sys.argv[3:]
O = os.environ.get('REG_S', '/tmp') + f'/ens/{tag}'; os.makedirs(O, exist_ok=True)
env = dict(os.environ)
C = []
def pr(ias, tr): return (f'pr{ias}_{tr}', 'pull+roll', ias, '0:0,0.05:1,7:1', f'0:0,0.05:1,{tr-0.05}:1,{tr}:-1,5:-1', tr)
def fa(ias, tr): return (f'fa{ias}_{tr}', 'fullaft', ias, '0:0,0.05:1,8:1', f'0:0,0.05:1,{tr-0.05}:1,{tr}:-1,7:-1', tr)
def lf(ias, tr): return (f'lf{ias}_{tr}', 'leaf', ias, '0:0,0.8:0,0.85:1,17:1', f'0:0,4:0,4.05:1,{tr-0.05}:1,{tr}:-1,15:-1', tr)
def hp(ias, tr): return (f'hp{ias}_{tr}', 'halfpull', ias, '0:0,0.05:0.5,7:0.5', f'0:0,1:0,1.05:1,{tr-0.05}:1,{tr}:-1,7:-1', tr)
for ias in (635, 645, 650, 655, 665):
    for tr in (0.7, 0.75, 0.8):
        C.append(pr(ias, tr))
for ias in (540, 556, 570):
    for tr in (0.7, 0.75, 0.8):
        C.append(pr(ias, tr))
for ias in (480, 500, 520):
    for tr in (2.95, 3.05, 3.15):
        C.append(fa(ias, tr))
for ias in (150, 160, 170):
    for tr in (8.95, 9.05, 9.15):
        C.append(lf(ias, tr))
for ias in (680, 700, 720):
    for tr in (2.95, 3.05, 3.15):
        C.append(hp(ias, tr))
def run(c):
    n, grp, ias, prof, rprof, tr = c; f = f'{O}/{n}.csv'
    if not os.path.exists(f):
        subprocess.run(['dotnet', B + '/bin/bench.dll', 'one', f'iasKmh={ias}', 'alt=1500', 'trimT=1', 'pull=0', 'hold=0', 'roll=0', f'post={tr+5}', 'throttle=1', f'prof={prof}', f'rprof={rprof}', f'csv={f}'] + sets, cwd=B, env=env, capture_output=True)
    d = pd.read_csv(f); pre = d[(d.t > tr - 0.2) & (d.t < tr)]; s0 = np.sign(pre.pS.mean()); w = d[(d.t >= tr) & (d.t < tr + 4.5)]
    z = w[w.pS * s0 <= 0]; tz = z.t.iloc[0] - tr if len(z) else np.nan
    pk = w.pS.abs().max() if not len(z) else w[w.t > tr + tz].pS.abs().max()
    r9 = w[(w.pS * s0 < 0) & (w.pS.abs() >= 0.9 * pk)]; t9 = r9.t.iloc[0] - tr if len(r9) else np.nan
    return dict(grp=f'{grp}{str(ias)[:1]}' if grp == 'pull+roll' else grp, run=n, pS0=abs(pre.pS.mean()), tZero=tz, t90=t9, newPk=pk, beta=w.beta.abs().max())
with ThreadPoolExecutor(2) as ex: rows = list(ex.map(run, C))
r = pd.DataFrame(rows); r.to_csv(f'{O}/runs.csv', index=False)
g = r.groupby('grp').agg(n=('run', 'size'), pS0=('pS0', 'median'), t90=('t90', 'median'), t90max=('t90', 'max'), newPk=('newPk', 'median'), pkMax=('newPk', 'max'), beta=('beta', 'median'), betaMax=('beta', 'max'))
print(tag, ' '.join(sets)); print(g.round(2).to_string())
