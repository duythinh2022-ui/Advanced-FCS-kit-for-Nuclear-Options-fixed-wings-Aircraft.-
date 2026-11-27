import sys,os,glob,pandas as pd,numpy as np
O=sys.argv[1]; rows=[]
for f in sorted(glob.glob(O+'/*.csv')):
    n=os.path.basename(f)[:-4]; d=pd.read_csv(f); r={'case':n}
    h=d[(d.t>3.5)&(d.t<5)]
    r['aHold']=h.alpha.mean(); r['aMax']=d.alpha.max(); r['bPk']=d.beta.abs().max()
    r['over']=r['aMax']-r['aHold']
    if '_lr_' in n or '_rev_' in n: r['pS']=d[(d.t>5.2)&(d.t<9)].p.abs().mean()
    if '_rel_' in n:
        a=d[d.t>6.02]; k=a[a.alpha<15]; r['tRec']=(k.t.iloc[0]-6.02) if len(k) else np.nan
    r['dep']=int(r['bPk']>15 or r['aMax']>r['aHold']+12 or d.alpha.max()>100)
    rows.append(r)
T=pd.DataFrame(rows).set_index('case'); pd.set_option('display.width',200)
print(T.round(1).to_string())
print('SUMMARY dep %d  bPk mean %.1f max %.1f  over max %.1f  hold mean %.1f'%(T.dep.sum(),T.bPk.mean(),T.bPk.max(),T.over.max(),T.aHold.mean()))
