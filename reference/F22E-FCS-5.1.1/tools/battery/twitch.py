import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *  # kit 5.0: paths relative to this tree
import pandas as pd, numpy as np, glob, sys, os
def rev(s, dt):
    d=np.diff(s)/dt; d[np.abs(d)<2]=0  # deg/s below 2 = holding
    sg=np.sign(d); sg=sg[sg!=0]
    return int((np.diff(sg)!=0).sum())
tags=sys.argv[1:]
for mode in ('off','on'):
    for t in tags:
        tot=0; T=0; acc=0
        for cg in ('-0.05','0.0','0.05'):
            for f in glob.glob(f'{RUNS}/{t}_{mode}_{cg}/*.csv'):
                d=pd.read_csv(f); dt=np.median(np.diff(d.t))
                for c in ('stabLcmd','stabRcmd'):
                    tot+=rev(d[c].values,dt)
                    acc+=np.abs(np.diff(d[c].values,2)).sum()/dt/dt
                T+=d.t.iloc[-1]-d.t.iloc[0]
        print(f'TVC {mode:3s} {t:6s} stab cmd reversals/s {tot/T:6.2f}  mean |cmd accel| {acc/T/2/len(d)*1:8.1f}')
