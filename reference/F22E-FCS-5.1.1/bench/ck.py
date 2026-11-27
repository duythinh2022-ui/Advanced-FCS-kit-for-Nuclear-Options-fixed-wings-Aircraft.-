# corkscrew release runner: ck.py tag ias alt hold [overrides...]  -> post-release q metrics
import subprocess,sys,os,pandas as pd,numpy as np
tag,ias,alt,hold=sys.argv[1],sys.argv[2],sys.argv[3],float(sys.argv[4]); extra=sys.argv[5:]
rel=hold
prof=f"0:1,{rel:.2f}:1,{rel+0.12:.2f}:0,{rel+6:.2f}:0"
a=['one',f'iasKmh={ias}',f'alt={alt}','trimT=2','throttle=1',f'prof={prof}',f'rprof={prof}','rollFromPull=0',f'hold={rel+5:.2f}','post=0',f'csv=runs/ck_{tag}.csv']
p=subprocess.run(['dotnet','bin/bench.dll']+a+extra,capture_output=True,text=True)
d=pd.read_csv(f'runs/ck_{tag}.csv'); d['t']-=d.t.min()
t1=d[d.sp>0.5].t.min(); tr=d[(d.t>t1)&(d.sp<0.05)].t.min()  # release time (stick back to 0)
w=d[(d.t>=tr)&(d.t<tr+2.5)]; pre=d[(d.t>tr-1)&(d.t<tr)]
dt=np.median(np.diff(d.t))
cap=w[w.q<=w.q.iloc[0]*0.2]   # after capture
dip=w[w.t>(cap.t.min() if len(cap) else tr)]
print(f"{tag}: pre AoA {pre.alpha.mean():.1f} ias {pre.ias.mean():.0f} p {pre.p.mean():.0f} r {pre.r.mean():.0f} | qMin {dip.q.min():.2f} qMed {dip.q[dip.t<tr+2].median():.2f} t<-2 {(dip.q<-2).sum()*dt:.2f}s t<-1 {(dip.q<-1).sum()*dt:.2f}s | stab L/R pre {pre.stabL.mean():.0f}/{pre.stabR.mean():.0f}")
