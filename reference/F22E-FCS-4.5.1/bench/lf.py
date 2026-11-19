# falling leaf: lf.py tag ias throttle [overrides] -> full aft 12 s with alternating full pedal, then release
import subprocess,sys,pandas as pd,numpy as np
tag,ias,thr=sys.argv[1],sys.argv[2],sys.argv[3]; extra=sys.argv[4:]
prof="0:1,12:1,12.12:0,17:0"
yp="0:0,3:0,3.1:1,5:1,5.2:-1,7:-1,7.2:1,9:1,9.2:-1,11:-1,11.2:0"
a=['one',f'iasKmh={ias}','alt=3000','trimT=2',f'throttle={thr}',f'prof={prof}','rprof=0:0','rollFromPull=0',f'yprof={yp}','hold=17','post=0',f'csv=runs/lf_{tag}.csv']
subprocess.run(['dotnet','bin/bench.dll']+a+extra,capture_output=True,text=True)
d=pd.read_csv(f'runs/lf_{tag}.csv'); d['t']-=d.t.min()
t0=d[d.sp>0.5].t.min(); tr=d[(d.t>t0)&(d.sp<0.05)].t.min(); dt=np.median(np.diff(d.t))
L=d[(d.t>t0+4)&(d.t<tr)]; R=d[(d.t>=tr)&(d.t<tr+3)]
cap=R[R.q<=R.q.iloc[0]*0.2]; dip=R[R.t>(cap.t.min() if len(cap) else tr)]
print(f"{tag}: leaf ias {L.ias.mean():.0f} AoA mean {L.alpha.mean():.1f} min {L.alpha.min():.1f} max {L.alpha.max():.1f} sd {L.alpha.std():.2f} | q min {L.q.min():.1f} max {L.q.max():.1f} | beta max {L.beta.abs().max():.1f} r max {L.r.abs().max():.0f} | release qMin {dip.q.min():.2f} t<-1 {(dip.q<-1).sum()*dt:.2f}s")
