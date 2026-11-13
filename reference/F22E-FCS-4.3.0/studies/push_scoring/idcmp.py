#!/usr/bin/env python3
# idcmp.py <tagA> <tagB>: per replay, are the flown states and commands identical? max abs diff of the behaviour columns
import sys, os, glob, numpy as np, pandas as pd
A=f'/home/claude/w/score/id_{sys.argv[1]}'; B=f'/home/claude/w/score/id_{sys.argv[2]}'
beh=['alpha_b','beta_b','p_b','q_b','r_b','nz_b','ias_b','phi_b','theta_b','stabL_b','stabR_b','rudL_b','rudR_b','tvc_b','ailL_b','flapL_b','hl_b','pCmd','rCmd','q_cmd','pS_b']
same=0; bytesame=0; diff=[]; n=0
for fa in sorted(glob.glob(A+'/*.csv')):
    fb=B+'/'+os.path.basename(fa)
    if not os.path.exists(fb): print('missing',fb); continue
    n+=1
    if open(fa,'rb').read()==open(fb,'rb').read(): bytesame+=1; same+=1; continue
    a=pd.read_csv(fa); b=pd.read_csv(fb)
    if len(a)!=len(b): diff.append((os.path.basename(fa),'len',len(a),len(b))); continue
    d=max(float(np.nanmax(np.abs(a[c].values-b[c].values))) for c in beh)
    if d==0: same+=1
    else:
        t1=float(a.t[(np.abs(a[beh].values-b[beh].values)>0).any(axis=1)].iloc[0])
        diff.append((os.path.basename(fa),round(d,3),'first diff t',t1,'beta max',round(a.beta_b.abs().max(),2),round(b.beta_b.abs().max(),2)))
print(f'{n} replays: {bytesame} byte-identical, {same} identical in the flown columns, {len(diff)} differ')
for x in diff: print(' ',*x)
