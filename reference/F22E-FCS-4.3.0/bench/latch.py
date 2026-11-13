import subprocess,sys,pandas as pd,numpy as np
tag=sys.argv[1]; extra=sys.argv[2:]
for ias in [250,330]:
  f=f'runs/latch_{tag}_{ias}.csv'
  prof='0:1,2:1,2.05:0.3,6:0.3,6.05:1,10:1'; rprof='0:-1,2:-1,2.05:0,6.0:0,6.05:-1,10:-1'
  subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50','throttle=1','trimT=2','hold=10','post=0','rollFromPull=0','rollT=10',f'iasKmh={ias}',f'prof={prof}',f'rprof={rprof}',f'csv={f}']+extra,capture_output=True)
  d=pd.read_csv(f); d['t']=d.t+10; w=d[d.t>=6]
  t20=w.t[w.alpha>20]
  print(tag,ias,'2nd pull: alpha at 6',round(w.alpha.iloc[0],1),'t20',round(t20.iloc[0]-6,2) if len(t20) else None,'bmax',round(w.beta.abs().max(),1),'gov at 6.1',round(w.slipGovPull.iloc[6] if 'slipGovPull' in w else -9,2))
