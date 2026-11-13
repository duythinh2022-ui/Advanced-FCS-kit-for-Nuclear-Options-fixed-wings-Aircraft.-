import subprocess,sys,os,pandas as pd,numpy as np
# replay stick from the 19 Oct log: replay.py t0 t1 tag [overrides]
L='/root/.claude/uploads/1f60cb6e-1a8a-5062-bd20-8c2d1385dbb1/4284a4f6-F22E_FCS_20261019_180523.csv'
t0,t1,tag=float(sys.argv[1]),float(sys.argv[2]),sys.argv[3]; extra=sys.argv[4:]
d=pd.read_csv(L,usecols=['t','sp','sr','sy','ias','tw','theta','alpha'])
s=d[(d.t>=t0)&(d.t<=t1)].iloc[::3]
f=lambda c:','.join(f'{t-t0:.2f}:{v:.2f}' for t,v in zip(s.t,s[c]))
r0=s.iloc[0]
a=['one','AoANeg=-50','trimT=2',f'iasKmh={r0.ias*3.6:.0f}',f'throttle={max(0.05,r0.tw):.2f}',f'prof={f("sp")}',f'rprof={f("sr")}',f'yprof={f("sy")}','rollFromPull=0',f'hold={t1-t0:.1f}','post=0',f'gamma0={r0.theta-r0.alpha:.0f}']
csv=f'runs/rep_{tag}.csv'; a.append(f'csv={csv}')
a[-1:-1]=[] 
p=subprocess.run(['dotnet','bin/bench.dll']+a+extra,capture_output=True,text=True); print('\n'.join(l for l in p.stdout.splitlines() if l.startswith('#')))
if not os.path.exists(csv): print(p.stdout[-2000:],p.stderr[-2000:]); sys.exit()
w=pd.read_csv(csv); w=w[w.t>=-(t1-t0)]
e=w.p-w.pCmd; print(tag,'bmax %.1f amax %.1f qTV %.0f qcTV %.0f tvcTV %.0f'%(w.beta.abs().max(),w.alpha.max(),w.q.diff().abs().sum()/(t1-t0),w.qCmd.diff().abs().sum()/(t1-t0),w.tvc.diff().abs().sum()/(t1-t0)))
