import pandas as pd, numpy as np, sys, warnings; warnings.filterwarnings('ignore')
R='/home/claude/su30/v10/src/bench/runs/'
rows=[]
for tag in sys.argv[1].split(','):
  for c,ias in [('ck',300),('ck',450),('ck',600),('pl',450),('pl',600),('p5',450)]:
    try: d=pd.read_csv(f'{R}{tag}_{c}_{ias}.csv').copy()
    except Exception as e: continue
    a=np.radians(d.alpha); d['pS']=d.p*np.cos(a)+d.r*np.sin(a); d['nozS']=(d.nozL+d.nozR)/2; d['nozD']=(d.nozL-d.nozR)/2
    x=d[d.t>0.4]
    on=x[(x.alpha>5)&(x.alpha<45)]
    wall=x[(x.alpha>45)&(x.alpha<66)&(x.qCmd>25)]
    t60=x[x.alpha>=55].t.min()-0.4 if (x.alpha>=55).any() else np.nan
    rows.append(dict(tag=tag,case=f'{c}{ias}',nozS_5_45=on.nozS.mean(),nozS_hi=(on.nozS>=12).mean(),pSon=on.pS.abs().mean(),qerr_on=(on.qCmd-on.q).clip(lower=0).mean(),
      t55=t60,wall_n=len(wall)/60,wall_q=wall.q.mean(),wall_pS=wall.pS.abs().mean(),wall_nozS=wall.nozS.mean(),wall_nozD=wall.nozD.abs().mean(),
      bMax=x.beta.abs().max(),aMax=x.alpha.max(),iasEnd=x.ias.iloc[-1]*3.6,qmax=x.q.max()))
pd.set_option('display.width',250); print(pd.DataFrame(rows).round(2).to_string(index=False))
