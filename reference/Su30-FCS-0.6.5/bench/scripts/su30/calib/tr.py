import sys,pandas as pd,numpy as np
R='/home/claude/su30/src/bench/runs/'
c=sys.argv[1]; tags=sys.argv[2].split(','); t0,t1,dt=[float(x) for x in sys.argv[3:6]]
D={k:pd.read_csv(f'{R}{k}_{c}.csv') for k in tags}
for k,d in D.items(): 
    a=np.radians(d.alpha); d['pS']=d.p*np.cos(a)+d.r*np.sin(a); d['nozS']=(d.nozL+d.nozR)/2; d['nozD']=(d.nozL-d.nozR)/2
print(c)
for t in np.arange(t0,t1+1e-6,dt):
    s=f"{t:5.1f}"
    for k,d in D.items():
        r=d.iloc[(d.t-t).abs().argmin()]
        s+=f" | {k}: AoA{r.alpha:5.1f} q{r.q:6.1f}/{r.qCmd:5.1f} vv{r.pS:6.1f} b{r.beta:5.1f} st{r.elevator_L_now:4.0f}/{r.elevator_R_now:4.0f} ca{r.canard_L_now:4.0f} nS{r.nozS:5.1f} nD{r.nozD:5.1f}"
    print(s)
