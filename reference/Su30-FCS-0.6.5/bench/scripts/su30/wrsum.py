import pandas as pd, numpy as np, sys, warnings; warnings.filterwarnings('ignore')
R='/home/claude/su30/v10/src/bench/runs/'
def hf(x): return (x - x.rolling(30,center=True,min_periods=1).mean()).std()
def rev(x,th=3):
  v=x.values; d=np.diff(v); s=np.sign(d); idx=np.where(np.abs(d)>1e-3)[0]; n=0; last=None; acc=0
  # count reversals with swing > th deg
  ext=v[0]; dirn=0; cnt=0
  for y in v[1:]:
    if dirn>=0 and y<ext-th: cnt+= (dirn>0); dirn=-1; ext=y
    elif dirn<=0 and y>ext+th: cnt+= (dirn<0); dirn=1; ext=y
    elif (dirn>=0 and y>ext) or (dirn<=0 and y<ext): ext=y
  return cnt
rows=[]
for tag in sys.argv[1].split(','):
  for c in ['rp','wr_1.6','wr_2.0','wc_350','wc_480','wc_600','wl_350','wl_480','wl_600','k350']:
    try: d=pd.read_csv(f'{R}{tag}_{c}.csv')
    except Exception as e: continue
    a=np.radians(d.alpha); d['pS']=d.p*np.cos(a)+d.r*np.sin(a)
    x=d[d.t>d.t.iloc[0]+0.5]
    lat=x[x.sr.abs()>0.1]
    w=lat[lat.alpha.cummax()>55] if len(lat) else lat
    rows.append(dict(tag=tag,case=c,aDipLat=(w.alpha.cummax()-w.alpha).max() if len(w) else np.nan,
      qMinLat=w.q.min() if len(w) else np.nan, pS=lat.pS.abs().mean() if len(lat) else np.nan,
      bMax=x.beta.abs().max(), aMax=x.alpha.max(), iasEnd=x.ias.iloc[-1]*3.6,
      hf_q=hf(x.q),hf_p=hf(x.p),hf_r=hf(x.r), revEL=rev(x.elevator_L_now)+rev(x.elevator_R_now), revCan=rev(x.canard_L_now)))
pd.set_option('display.width',250); print(pd.DataFrame(rows).sort_values(['case','tag']).round(2).to_string(index=False))
