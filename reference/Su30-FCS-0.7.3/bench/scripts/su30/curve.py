# curve quality on any FCS csv set: high-frequency residual of rates, actuator chatter (reversals > 3 deg per s)
import pandas as pd, numpy as np, sys, glob, warnings, os; warnings.filterwarnings('ignore')
def hf(x,n=30): return float((x - x.rolling(n,center=True,min_periods=1).mean()).std())
def chatter(x,amp=3.0):
  v=x.values; ext=[v[0]]; dirn=0; cnt=0
  for a in v[1:]:
    if dirn>=0 and a>ext[-1]: ext[-1]=a; dirn=1; continue
    if dirn<=0 and a<ext[-1]: ext[-1]=a; dirn=-1; continue
    if abs(a-ext[-1])>=amp: cnt+=1; ext.append(a); dirn=-dirn if dirn!=0 else (1 if a>ext[-2] else -1)
  return cnt
def score(f):
  d=pd.read_csv(f)
  if "t" not in d or "nozL" not in d: return None
  x=d[d.t>0.5]; T=max(1e-3,x.t.iloc[-1]-x.t.iloc[0])
  out=dict(hf_p=hf(x.p),hf_q=hf(x.q),hf_r=hf(x.r))
  for k,c in [('el','elevator_L_now'),('can','canard_L_now'),('rud','rudder_L_now'),('fl','flaperon_L_now'),('nzL','nozL')]:
    out['ch_'+k]=chatter(x[c])/T
  return out
rows=[]
for spec in sys.argv[1:]:
  tag,pat=spec.split('=',1)
  fs=sorted(glob.glob(pat))
  sc=[s for s in (score(f) for f in fs) if s]
  if not sc: continue
  df=pd.DataFrame(sc); r=df.mean().to_dict(); r['tag']=tag; r['n']=len(fs); r['worst_hf_q']=df.hf_q.max(); r['worst_ch_noz']=df.ch_nzL.max(); rows.append(r)
pd.set_option('display.width',250); print(pd.DataFrame(rows).set_index('tag').round(2).to_string())
