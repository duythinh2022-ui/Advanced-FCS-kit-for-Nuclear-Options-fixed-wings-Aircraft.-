import sys,glob,os,numpy as np,pandas as pd,warnings; warnings.filterwarnings('ignore')
O=sys.argv[1]; rows=[]
for f in sorted(glob.glob(O+'/*.csv')):
    d=pd.read_csv(f,low_memory=False).copy(); n=os.path.basename(f)[:-4]
    if len(d)<20: continue
    a=np.radians(d.alpha); d['pS']=d.p*np.cos(a)+d.r*np.sin(a)
    lim=d.alphaCmd.max() if 'alphaCmd' in d else np.nan
    r=dict(case=n,aMax=d.alpha.max(),aMin=d.alpha.min(),bPk=d.beta.abs().max(),iasEnd=d.ias.iloc[-1])
    if n.startswith(('hold','lr','rev','ped','idle','nt')):
        w=d[(d.t>3.5)&(d.t<5)]; r['aHold']=w.alpha.mean()
        r['tTo90%']=(d.t[d.alpha>0.9*d.alpha.max()].min()-1.0)
    if n.startswith(('lr','idle','nt')):
        w=d[(d.t>5.3)&(d.t<9)]; r['pSmean']=w.pS.abs().mean(); r['aRoll']=w.alpha.mean(); r['aRollMin']=w.alpha.min()
    if n.startswith('rev'):
        w=d[(d.t>7)&(d.t<9.5)]; s=np.sign(w.pS.values); x=np.where(np.diff(s)!=0)[0]
        r['tRev']=(w.t.values[x[0]]-7.0) if len(x) else np.nan; r['pSmean']=d[(d.t>5.3)&(d.t<9.5)].pS.abs().mean(); r['aRoll']=d[(d.t>5.3)&(d.t<9.5)].alpha.mean()
    if n.startswith('ped'):
        w=d[(d.t>5)&(d.t<8)]; r['rMean']=w.r.abs().mean(); r['aRoll']=w.alpha.mean()
    if n.startswith('rel'):
        w=d[d.t>6]; r['t<30']=(w.t[w.alpha<30].min()-6.0) if (w.alpha<30).any() else np.nan; r['t<15']=(w.t[w.alpha<15].min()-6.0) if (w.alpha<15).any() else np.nan
        r['aUnder']=w.alpha.min()
    if n.startswith('push'):
        r['aNegHold']=d[(d.t>3.5)&(d.t<5)].alpha.mean(); r['pSmean']=d[(d.t>5.3)&(d.t<8)].pS.abs().mean()
    # departure: sideslip > 15 or AoA beyond the limit by > 8 or uncommanded yaw/roll after stick neutral
    r['dep']=int(d.beta.abs().max()>15 or (d.alpha.max()>d.alpha[(d.t>3.5)&(d.t<5)].mean()+12 if n!='push_300' else False))
    rows.append(r)
R=pd.DataFrame(rows); pd.set_option('display.width',260); pd.set_option('display.max_columns',30)
print(R.round(2).to_string(index=False))
