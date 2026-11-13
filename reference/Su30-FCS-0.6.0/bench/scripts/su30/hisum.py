import pandas as pd, numpy as np, sys, warnings; warnings.filterwarnings('ignore')
tags=sys.argv[1].split(','); R='/home/claude/su30/v10/src/bench/runs/'
def load(f):
    d=pd.read_csv(f); a=np.radians(d.alpha); d['vv']=d.p*np.cos(a)+d.r*np.sin(a); return d
rows=[]
for c,t0,t1 in [('pr',0.6,6.05),('hr',4.05,8.05),('rev',4.05,7.05)]:
  for ias in [350,450]:
    for tag in tags:
      try: d=load(f'{R}{tag}_{c}_{ias}.csv')
      except Exception as e: print(tag,c,ias,'missing'); continue
      x=d[(d.t>=t0)&(d.t<t1)]; y=d[(d.t>=t1)&(d.t<t1+3)]
      # time to 90 deg of vv roll
      ang=np.cumsum(-x.vv.values*0.02 if c!='rev' else np.abs(x.vv.values)*0.02)
      t90=x.t.values[np.argmax(ang>=90)]-t0 if (ang>=90).any() else np.nan
      stop=y[y.t<t1+1.5]
      rows.append(dict(case=c,ias=ias,tag=tag,vvMax=np.abs(x.vv).max(),vvMean=np.abs(x.vv).mean(),t90=t90,
        aMin=x[x.t>t0+1.5].alpha.min() if c=='pr' else x.alpha.min(),aMax=x.alpha.max(),bRoll=x.beta.abs().max(),
        bAfter=y.beta.abs().max(),vvAfter1=np.abs(y[(y.t>t1+0.8)&(y.t<t1+1.2)].vv).mean(),diffAbs=x.tvcdNow.abs().mean(),
        iasEnd=d.ias.iloc[-1]*3.6))
print(pd.DataFrame(rows).to_string(float_format=lambda v:'%.1f'%v))
