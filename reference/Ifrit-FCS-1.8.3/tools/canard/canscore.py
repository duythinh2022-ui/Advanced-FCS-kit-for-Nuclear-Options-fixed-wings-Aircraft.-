import sys,glob,os,numpy as np,pandas as pd,warnings; warnings.filterwarnings('ignore')
O=sys.argv[1]; rows=[]
for f in sorted(glob.glob(O+'/*.csv')):
    d=pd.read_csv(f,low_memory=False)
    if len(d)<10: continue
    n=os.path.basename(f)[:-4]
    a=np.radians(d.alpha); pS=d.p*np.cos(a)+d.r*np.sin(a)
    dt=np.median(np.diff(d.t)); pdot=np.gradient(pS,dt)
    can=[c for c in d.columns if c.endswith('_aoaNow') and (('canard' in c) or ('(1)' in c))]
    stab=[c for c in d.columns if c.endswith('_aoaNow') and ('levator' in c) and '(1)' not in c]
    def stallfrac(cols,lim):
        if not cols: return np.nan
        return float(np.mean(np.max(np.abs(d[cols].values),axis=1)>lim))
    # settle: after last stick input, time until the pitch-surface differential is < 3 deg
    pairs=[]
    for L,Rr in [('canard_L','canard_R'),('elevator_L (1)','elevator_R (1)'),('Elevator_L','Elevator_R')]:
        if L+'_now' in d.columns: pairs.append((L,Rr))
    lastin=d.t[(d.sr.abs()>0.05)|(d.sy.abs()>0.05)].max() if ((d.sr.abs()>0.05)|(d.sy.abs()>0.05)).any() else np.nan
    sett={}
    for L,Rr in pairs:
        diff=(d[L+'_now']-d[Rr+'_now']).abs()/2
        if np.isnan(lastin): sett[L]=np.nan; continue
        after=d[d.t>lastin+0.3]; dd=diff[d.t>lastin+0.3]
        bad=after.t[dd>3.0]; sett[L]=(bad.max()-lastin) if len(bad) else 0.0
    rows.append(dict(case=n,bPk=d.beta.abs().max(),aMax=d.alpha.max(),aMin=d.alpha.min(),pSmean=pS[(d.sr.abs()>0.5)].abs().mean() if (d.sr.abs()>0.5).any() else np.nan,
        pdotRms=np.sqrt(np.mean(np.diff(pdot)**2)),canStall=stallfrac(can,28),stabStall=stallfrac(stab,28),
        settle=';'.join(f'{k.split("_")[0][:3]}{"c" if "(1)" in k or "canard" in k else "t"}:{v:.2f}' for k,v in sett.items()), stallC=d['stallCost'].max() if 'stallCost' in d else np.nan))
R=pd.DataFrame(rows); pd.set_option('display.width',250)
print(R.round(2).to_string(index=False))
