import pandas as pd, numpy as np, sys
f=sys.argv[1]; d=pd.read_csv(f); a=d[d['mode']==3].copy().reset_index(drop=True)
if 'paceHold' not in a: a['paceHold']=0.0
for c in ['p','q','r','beta']:
    a[c+'_w']=a[c]-a[c].rolling(19,center=True,min_periods=5).mean()   # 0.3 s detrend: residual = wobble
w=a.groupby((a.t//1.0)*1.0)
g=w.agg(alpha_min=('alpha','min'),alpha_max=('alpha','max'),ias=('ias','mean'),p=('p',lambda x:x.abs().max()),q=('q',lambda x:x.abs().max()),r=('r',lambda x:x.abs().max()),beta=('beta',lambda x:x.abs().max()),
  pw=('p_w',lambda x: np.sqrt((x**2).mean())),qw=('q_w',lambda x: np.sqrt((x**2).mean())),rw=('r_w',lambda x: np.sqrt((x**2).mean())),bw=('beta_w',lambda x: np.sqrt((x**2).mean())),hold=('paceHold',lambda x:(x.abs()>1).mean()))
pd.set_option('display.width',250); pd.set_option('display.max_rows',400)
print(f,'overall wobble rms p/q/r/beta: %.2f %.2f %.2f %.2f'%tuple(np.sqrt((a[c+'_w']**2).mean()) for c in ['p','q','r','beta']))
print('frac of seconds with q wobble >3: %.3f, r wobble >2: %.3f, p wobble >6: %.3f'%((g.qw>3).mean(),(g.rw>2).mean(),(g.pw>6).mean()))
if len(sys.argv)>2: print(g.sort_values(sys.argv[2],ascending=False).head(15).round(2).to_string())
