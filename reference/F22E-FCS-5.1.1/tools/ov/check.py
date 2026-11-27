# check.py <tag> <cg> : option A's model check - the plan at the 40 / 45 deg crossings against the braking achieved over the last 5 deg
import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *; import sys  # kit 5.0: paths relative to this tree
import pandas as pd, numpy as np, json, sys
tag, cg = sys.argv[1], sys.argv[2]
W = json.load(open(OV+"/windows.json"))
out=[]
for w in W:
    d = pd.read_csv(f"{OV}/out/{tag}_{cg}/w{w['id']:02d}.csv")
    t=d.t.values; a=d.alpha_b.values; q=d.q_b.values
    qs = pd.Series(q).rolling(6, center=True, min_periods=1).mean().values
    qd = np.gradient(qs, t)
    ip = int(np.argmax(a)); pk=a[ip]
    if pk < 47: continue
    rec=dict(id=w['id'], pk=pk, roll=np.abs(d.sr.values[:ip+1]).max())
    for c in (40,45):
        ix=[i for i in range(1,ip+1) if a[i-1]<c<=a[i]]
        if not ix: rec[f'p{c}']=np.nan; continue
        i=ix[-1]
        for k in ['aBrakePlan_b','planANet','planRollTake','planCoupArr','aBrakeTgt_b','aDn_b','planQRatio']: rec[f'{k}@{c}']=d[k].values[i]
        rec[f'p{c}']=d.aBrakePlan_b.values[i]
    seg=[i for i in range(ip+1) if a[i]>=pk-5 and q[i]>d.capBoundU_b.values[i] and qd[i]<0]
    seg_all=[i for i in range(ip+1) if a[i]>=pk-5]
    rec['ach']=-qd[seg].mean() if len(seg)>=3 else np.nan
    rec['ach_all']=-qd[seg_all].mean() if seg_all else np.nan
    rec['nseg']=len(seg)
    rec['aDn_last5']=d.aDn_b.values[seg_all].mean()
    out.append(rec)
R=pd.DataFrame(out)
pd.set_option('display.width',250)
for c in (40,45):
    ok=R[[f'p{c}','ach']].dropna()
    e=(ok[f'p{c}']-ok.ach)/ok.ach
    netonly=(R.loc[ok.index,f'planANet@{c}']-ok.ach)/ok.ach
    print(f"CG {cg} @{c}: n={len(ok)} plan/achieved-1: median {e.median()*100:+.0f}%  plans >1.5x achieved: {(ok[f'p{c}']>1.5*ok.ach).sum()}  | aNet alone median {netonly.median()*100:+.0f}%  | achieved median {ok.ach.median():.1f} deg/s^2, live aDn last 5 deg median {R.loc[ok.index,'aDn_last5'].median():.1f}")
cols=['id','pk','roll','ach','aDn_last5','p40','planANet@40','planRollTake@40','planCoupArr@40','planQRatio@40','aBrakeTgt_b@40','p45','planANet@45','aBrakeTgt_b@45']
print(R[cols].round(1).to_string())
