import sys,glob,os,pandas as pd,numpy as np
L="/mnt/user-data/uploads/Nuclear Option/BepInEx/Su30_FCS_20261109_"
R='/home/claude/su30/src/bench/runs/val/'
print('window            tag   AoA rms | q rms | p rms | r rms | IAS rms | peak AoA g/b | min AoA after peak g/b')
tags=sys.argv[1:]
keys=sorted({os.path.basename(f).split('_',1)[1] for f in glob.glob(R+tags[0]+'_*.csv')})
G={}
for k in keys:
    t=k.split('_')[0]
    G.setdefault(t,pd.read_csv(L+t+'.csv',low_memory=False))
    g=G[t]
    for tag in tags:
        b=pd.read_csv(R+tag+'_'+k,low_memory=False); gw=g[(g.t>=b.t.min())&(g.t<=b.t.max())]
        o=[]
        for c,sc in [('alpha',1),('q',1),('p',1),('r',1),('ias',3.6)]:
            gi=np.interp(b.t,gw.t,gw[c]); o.append(np.sqrt(((b[c]-gi)**2).mean())*sc)
        pk=lambda x:(x.alpha.max(), x.alpha[x.alpha.idxmax():].min())
        print(f"{k[:-4]:16s} {tag:5s} {o[0]:6.1f} | {o[1]:5.1f} | {o[2]:5.1f} | {o[3]:5.1f} | {o[4]:6.1f} | {gw.alpha.max():.0f}/{b.alpha.max():.0f} | {pk(gw)[1]:.0f}/{pk(b)[1]:.0f}")
