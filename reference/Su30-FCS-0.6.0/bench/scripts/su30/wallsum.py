import pandas as pd, numpy as np, sys, warnings; warnings.filterwarnings('ignore')
R='/home/claude/su30/v10/src/bench/runs/'
def hf(x): return (x - x.rolling(30,center=True,min_periods=1).mean()).std()
rows=[]
for tag in sys.argv[1].split(','):
  for c in ['wl','wc']:
    for ias in [350,480,600]:
      try: d=pd.read_csv(f'{R}{tag}_{c}_{ias}.csv')
      except: continue
      a=np.radians(d.alpha); d['pS']=d.p*np.cos(a)+d.r*np.sin(a)
      x=d[d.t>0.5]
      # time from 45 deg to 70 deg
      t45=x[x.alpha>=45].t.min(); t70=x[x.alpha>=70].t.min()
      w=x[(x.alpha>45)&(x.t<(t70 if t70==t70 else 99))]
      stall=((w.q<0.5*w.qCmd)&(w.qCmd>15)).sum()/60
      rows.append(dict(tag=tag,case=f'{c}{ias}',t45_70=(t70-t45),stall_s=stall,qmin_45_70=w.q.min() if len(w) else np.nan,aDip=(w.alpha.cummax()-w.alpha).max() if len(w) else np.nan,
        pS_wall=w.pS.abs().mean() if c=='wc' else np.nan, pSmin_wall=w.pS.abs().min() if c=='wc' and len(w) else np.nan,
        hf_q=hf(x.q),hf_p=hf(x.p),hf_r=hf(x.r),hf_el=hf(x.elevator_L_now),hf_noz=hf((x.nozL+x.nozR)/2),bMax=x.beta.abs().max(),aMax=x.alpha.max()))
pd.set_option('display.width',250); print(pd.DataFrame(rows).round(2).to_string(index=False))
