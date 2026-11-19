import subprocess,sys,os,pandas as pd,numpy as np
tag,ias,alt,rel,thr=sys.argv[1],sys.argv[2],sys.argv[3],float(sys.argv[4]),sys.argv[5]; extra=sys.argv[6:]
pr=f"0:0,0.2:1,{rel}:1,{rel+0.12:.2f}:0"
env=dict(os.environ,BENCH_DATA='/root/w/ifrit/ifrit_airframe.json')
subprocess.run(['dotnet','bin/bench.dll','ifly',f'alt={alt}','FuelFrac=1','ThrustScale=1.08','DragScale=0.95',f'ias={ias}',f'thr={thr}',f'T={rel+5}',f'pitch={pr}',f'roll={pr}',f'csv=runs/ick_{tag}.csv']+extra,capture_output=True,env=env)
d=pd.read_csv(f'runs/ick_{tag}.csv'); dt=np.median(np.diff(d.t)); tr=rel+0.12
w=d[(d.t>=tr)&(d.t<tr+2.5)]; pre=d[(d.t>tr-1)&(d.t<tr-0.12)]
cap=w[w.q<=w.q.iloc[0]*0.2]; dip=w[w.t>(cap.t.min() if len(cap) else tr)]
print(f"{tag}: pre AoA {pre.alpha.mean():.1f} ias {pre.ias.mean():.0f} r {pre.r.mean():.0f} | qMin {dip.q.min():.2f} qMed {dip.q[dip.t<tr+2].median():.2f} t<-2 {(dip.q<-2).sum()*dt:.2f}s t<-1 {(dip.q<-1).sum()*dt:.2f}s")
