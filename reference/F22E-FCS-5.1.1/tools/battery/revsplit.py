import sys,glob,os,pandas as pd,numpy as np
# reversed-split rows (stab split opposing roll stick) at |AoA|<20 and |sr|>0.5, plus a stab-chatter index
def stats(dirn):
    tot=0; n=0; ch=0
    for f in sorted(glob.glob(dirn+'/*.csv')):
        d=pd.read_csv(f)
        if 'stabL' not in d: continue
        sr=d['rS'] if 'rS' in d else None
        split=d.stabL-d.stabR
        sri=d['sr'] if 'sr' in d else None
        if sri is None: continue
        m=(np.sign(split)==np.sign(sri))&(split.abs()>6)&(sri.abs()>0.5)&(d.alpha.abs()<20)
        tot+=m.sum(); n+=len(d)
        ch+=np.abs(np.diff(d.stabL.values,2)).sum()+np.abs(np.diff(d.stabR.values,2)).sum()
    return tot,n,ch
for a in sys.argv[1:]: print(a, stats(a))
