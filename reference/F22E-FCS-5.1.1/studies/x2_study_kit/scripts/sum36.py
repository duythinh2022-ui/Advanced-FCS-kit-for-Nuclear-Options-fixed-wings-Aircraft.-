import os
import csv,sys,math
for f in sys.argv[1:]:
    r=[{k:float(v) for k,v in x.items() if v not in ('',)} for x in csv.DictReader(open(f))]
    t0=-4.5  # roll start (pull at -6, roll 1.5 s later)
    on=[x for x in r if t0<=x['t']<t0+0.6]; ss=[x for x in r if t0+1.5<=x['t']<t0+4.5]
    m=lambda a,k: sum(x[k] for x in a)/len(a)
    pr=lambda a,k: (min(x[k] for x in a), max(x[k] for x in a))
    wv=lambda x: x['p']*math.cos(math.radians(x['alpha']))+x['r']*math.sin(math.radians(x['alpha']))
    print(f"{f.split('/')[-1]}: IAS {m(ss,'ias'):.0f} AoA {m(ss,'alpha'):.1f} beta max {max(abs(x['beta']) for x in r if x['t']>=t0):.1f} | roll about vel {sum(wv(x) for x in ss)/len(ss):.0f} deg/s (onset 0.6s mean {sum(wv(x) for x in on)/len(on):.0f})")
    print(f"   steady: stabL {m(ss,'stabL'):+.1f} (local {m(ss,'sL_laoa'):.1f})  stabR {m(ss,'stabR'):+.1f} (local {m(ss,'sR_laoa'):.1f})  TVC mean {m(ss,'tvc'):+.1f} range {pr(ss,'tvc')[0]:+.1f}..{pr(ss,'tvc')[1]:+.1f}  rud {m(ss,'rudL'):+.1f}/{m(ss,'rudR'):+.1f}  thr {m(ss,'thr'):.2f}")
    print(f"   onset : TVC range {pr(on,'tvc')[0]:+.1f}..{pr(on,'tvc')[1]:+.1f}  stabL {pr(on,'stabL')[0]:+.1f}..{pr(on,'stabL')[1]:+.1f}  stabR {pr(on,'stabR')[0]:+.1f}..{pr(on,'stabR')[1]:+.1f}  q range {pr(on,'q')[0]:+.1f}..{pr(on,'q')[1]:+.1f}")
