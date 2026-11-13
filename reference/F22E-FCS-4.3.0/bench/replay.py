import subprocess,sys,os,pandas as pd,numpy as np
# replay stick from the 19 Oct log: replay.py t0 t1 tag [overrides]
L='/root/.claude/uploads/1f60cb6e-1a8a-5062-bd20-8c2d1385dbb1/db6f49df-F22E_FCS_20261019_085602.csv'
t0,t1,tag=float(sys.argv[1]),float(sys.argv[2]),sys.argv[3]; extra=sys.argv[4:]
d=pd.read_csv(L,usecols=['t','sp','sr','sy','ias','tw','theta','alpha'])
s=d[(d.t>=t0)&(d.t<=t1)].iloc[::3]
f=lambda c:','.join(f'{t-t0:.2f}:{v:.2f}' for t,v in zip(s.t,s[c]))
r0=s.iloc[0]
a=['one','AoANeg=-50','trimT=2',f'iasKmh={r0.ias*3.6:.0f}',f'throttle={max(0.05,r0.tw):.2f}',f'prof={f("sp")}',f'rprof={f("sr")}',f'yprof={f("sy")}','rollFromPull=0',f'hold={t1-t0:.1f}','post=0',f'gamma0={r0.theta-r0.alpha:.0f}']
csv=f'runs/rep_{tag}.csv'; a.append(f'csv={csv}')
a[-1:-1]=[] 
p=subprocess.run(['dotnet','bin/bench.dll']+a+extra,capture_output=True,text=True)
if not os.path.exists(csv): print(p.stdout[-2000:],p.stderr[-2000:]); sys.exit()
w=pd.read_csv(csv); w=w[w.t>=-(t1-t0)]
print(tag,'len',round(w.t.iloc[-1],1),'bmax',round(w.beta.abs().max(),1),'amax',round(w.alpha.max(),1),'amin',round(w.alpha.min(),1),'qsd',round(w.q.diff().abs().mean()*60,1),'tvc flips',int(((w.tvcNow.diff().abs())>0.5).sum()) if 'tvcNow' in w else -1,'decel',round(w.decel.mean(),2))
