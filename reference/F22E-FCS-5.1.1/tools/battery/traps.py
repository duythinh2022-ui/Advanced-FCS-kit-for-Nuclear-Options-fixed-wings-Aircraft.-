import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *  # kit 5.0: paths relative to this tree
import subprocess,sys,os,pandas as pd,numpy as np
B=BENCH; L=LOGDIR+'/F22E_FCS_20261126_'
env=dict(os.environ)
cases=[('212151',24.3),('213634',72.98),('201044',396.4),('213634',166.6)]
ex=sys.argv[1:]
for lg,tf in cases:
  o=f'/tmp/trap_{lg}_{tf}.csv'
  r=subprocess.run(['dotnet',B+'/bin/bench.dll','replay',f'log={L}{lg}.csv',f't0={tf-3.3}',f'pinUntil={tf}',f't1={tf+2.7}','forceCmd=1','pinSurf=1',f'out={o}','DisableTVC=true']+ex,cwd=B,env=env,capture_output=True,text=True)
  if r.returncode: print(r.stderr[-500:]); continue
  w=pd.read_csv(o); e=w[w.t>tf].reset_index(drop=True)
  u=np.hypot(e.unmetX_b,e.unmetZ_b); rm=u.rolling(30).mean()
  i=int(rm.idxmax()) if rm.notna().any() else 0
  print(f'{lg} {tf}: peak 0.5s unmet {rm.max():6.1f} at {e.t[i]:.2f}  mean {u.mean():5.1f}  stabs there {e.stabL_b[i]:6.1f}/{e.stabR_b[i]:6.1f}')
