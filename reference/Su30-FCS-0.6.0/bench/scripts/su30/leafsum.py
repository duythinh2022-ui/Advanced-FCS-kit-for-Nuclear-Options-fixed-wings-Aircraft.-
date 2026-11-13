import pandas as pd, numpy as np, sys, warnings; warnings.filterwarnings('ignore')
tags=sys.argv[1].split(','); R='/home/claude/su30/v10/src/bench/runs/'
rows=[]
for ias in [250,350]:
  for tag in tags:
    try: d=pd.read_csv(f'{R}{tag}_lf_{ias}.csv')
    except: print(tag,ias,'missing'); continue
    a=np.radians(d.alpha); d['vv']=d.p*np.cos(a)+d.r*np.sin(a)
    st=d[(d.t>8)&(d.t<14)]; en=d[(d.t>0.4)&(d.t<8)]; rv=d[(d.t>14)&(d.t<20)]; rl=d[(d.t>20)&(d.t<24)]
    t50=en.t.values[np.argmax(en.vv.abs().values>=50)] if (en.vv.abs()>=50).any() else np.nan
    rvs=rv[rv.t>17]
    rows.append(dict(ias=ias,tag=tag,vvSt=st.vv.abs().mean(),vvMin=st.vv.abs().min(),iasSt=st.ias.mean()*3.6,aSt=st.alpha.mean(),aMax=d.alpha.max(),
      bEntry=en.beta.abs().max(),bSt=st.beta.abs().max(),bRev=rv.beta.abs().max(),vvRev=rvs.vv.abs().mean(),bRel=rl.beta.abs().max(),
      vvRel1=rl[(rl.t>21)&(rl.t<21.5)].vv.abs().mean(),t50=t50,rMax=st.rMax.mean(),nozD=(st.nozL-st.nozR).abs().mean()/2,nozS=((st.nozL+st.nozR)/2).mean()))
print(pd.DataFrame(rows).to_string(float_format=lambda v:'%.1f'%v))
