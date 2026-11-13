import subprocess,sys,pandas as pd,os
tag=sys.argv[1]; ex=sys.argv[2:]; out=[]
for ias in [250,330,400,450,550,620,700]:
  f=f'runs/pr_{tag}_{ias}.csv'
  if not os.path.exists(f): subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50',f'iasKmh={ias}','trimT=1','pull=1','hold=4','rollFromPull=0','roll=1','rollT=4','throttle=1','post=0',f'csv={f}']+ex,capture_output=True)
  d=pd.read_csv(f); w=d[d.t>=-4]
  out.append((ias,round(w.alpha.max(),1),round(w.beta.abs().max(),1),round(w.q.diff().abs().sum()/4,0),round(w.qCmd.diff().abs().sum()/4,0)))
print(tag,out)
