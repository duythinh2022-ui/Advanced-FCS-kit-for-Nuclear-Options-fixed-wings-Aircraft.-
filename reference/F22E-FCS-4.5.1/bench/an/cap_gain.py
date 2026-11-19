#!/usr/bin/env python3
# Offline identification of the achieved/modelled gain per axis from the six 2.7.x logs.
# y = measured body angular acceleration (SG derivative of logged p/r), x = FCS table moment at the logged deflections.
import sys, glob, numpy as np, pandas as pd
from scipy.signal import savgol_filter
S='/tmp/claude-0/-home-claude/c3114abd-20fc-5984-be11-c25d56f03f2d/scratchpad'
def load(tag):
    d=pd.read_csv(f'{S}/logs/F22E_FCS_{tag}.csv'); m=pd.read_csv(f'{S}/mom/F22E_FCS_{tag}.mom.csv')
    d=d[d['mode'].isin([3,6])&(d.tas>40)].reset_index(drop=True)
    # SG derivative on the full log (contiguous rows at 1/60); mom rows are a subset with the same t
    dt=1/60.
    for c in 'pqr':
        d[c+'dot']=savgol_filter(d[c].values,9,3,deriv=1,delta=dt)
    mm=m.merge(d[['t','pdot','qdot','rdot','ias','nz','sr','sy','sp','yawSat','unmetZ','unmetX','capZP','capZN','capXP','capXN','aYaw','aRoll','rudderAuth','pCmd','rCmd','pSTarget','pSmax','rMax','rS']],on='t',how='inner')
    mm['tag']=tag; mm['qbar']=0.5*1.225*(mm.ias)**2  # ias m/s -> equivalent q
    return mm
tags=['20261102_095544','20261102_095730','20261102_101342','20261102_111133','20261102_113022','20261102_115058']
D=pd.concat([load(t) for t in tags],ignore_index=True)
# drop spikes (respawn etc.)
D=D[(D.rdot.abs()<600)&(D.pdot.abs()<3000)]
print('samples',len(D))
bands=[(-60,-30),(-30,-10),(-10,10),(10,20),(20,30),(30,40),(40,50),(50,60),(60,90)]
def fit(y,X):
    X=np.column_stack(X); beta,res,rk,sv=np.linalg.lstsq(X,y,rcond=None); pred=X@beta
    return beta, np.sqrt(np.mean((y-pred)**2))
print('\n== YAW: rdot vs table moments by AoA band ==')
print('band        n    rms(y)  rms(d=y-mNow)  rms(fit1)  gain1  | rms(fit3) gRud gStab gFlap gTvc | passive beta,r,p coeffs')
for lo,hi in bands:
    s=D[(D.alpha>=lo)&(D.alpha<hi)]
    if len(s)<200: continue
    y=s.rdot.values; ones=np.ones(len(s))
    pas=[ones, s.beta.values*s.qbar.values/1e4, s.r.values*s.ias.values/100, s.p.values*s.ias.values/100]
    b1,r1=fit(y,[s.mNowZ.values]+pas)
    b3,r3=fit(y,[s.rudZnow.values,s.stabZnow.values,s.flapZnow.values,s.tvcZnow.values]+pas)
    print(f'{lo:4d}..{hi:3d} {len(s):6d} {y.std():7.1f} {(y-s.mNowZ).std():8.1f}      {r1:7.1f}  {b1[0]:5.2f}  | {r3:7.1f} {b3[0]:5.2f} {b3[1]:5.2f} {b3[2]:5.2f} {b3[3]:5.2f} | {b3[5]:6.2f} {b3[6]:6.2f} {b3[7]:6.2f}')
print('\n== ROLL: pdot vs table moments by AoA band ==')
print('band        n    rms(y)  rms(d=y-mNow)  rms(fit1)  gain1  | rms(fit3) gRud gStab gFlap | passive beta,r,p')
for lo,hi in bands:
    s=D[(D.alpha>=lo)&(D.alpha<hi)]
    if len(s)<200: continue
    y=s.pdot.values; ones=np.ones(len(s))
    pas=[ones, s.beta.values*s.qbar.values/1e4, s.r.values*s.ias.values/100, s.p.values*s.ias.values/100]
    b1,r1=fit(y,[s.mNowX.values]+pas)
    b3,r3=fit(y,[s.rudXnow.values,s.stabXnow.values,s.flapXnow.values]+pas)
    print(f'{lo:4d}..{hi:3d} {len(s):6d} {y.std():7.1f} {(y-s.mNowX).std():8.1f}      {r1:7.1f}  {b1[0]:5.2f}  | {r3:7.1f} {b3[0]:5.2f} {b3[1]:5.2f} {b3[2]:5.2f} | {b3[4]:6.2f} {b3[5]:6.2f} {b3[6]:6.2f}')
D.to_pickle(f'{S}/D.pkl')
