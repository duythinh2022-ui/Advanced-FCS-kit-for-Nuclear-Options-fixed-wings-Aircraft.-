import pandas as pd, sys, subprocess, csv
d=pd.read_csv('/mnt/user-data/uploads/Nuclear Option/BepInEx/F22E_FCS_20261006_100035.csv',usecols=['t','sp','sr','ias','alpha'])
t0,t1=float(sys.argv[1]),float(sys.argv[2]); extra=sys.argv[3:]
w=d[(d.t>=t0)&(d.t<=t1)].iloc[::5]
prof=','.join(f"{r.t-t0:.2f}:{r.sp:.2f}" for r in w.itertuples())
rprof=','.join(f"{r.t-t0:.2f}:{-r.sr:.2f}" for r in w.itertuples())
ias=w.ias.iloc[0]*3.6
args=['dotnet','bin/bench.dll','one',f'iasKmh={ias:.0f}','trimT=1','throttle=0','post=0',f'hold={t1-t0:.1f}',f'prof={prof}',f'rprof={rprof}','roll=0','csv=/tmp/claude-0/rep.csv']+extra
subprocess.run(args,cwd='/home/claude/w/src/bench',capture_output=True)
D=list(csv.DictReader(open('/tmp/claude-0/rep.csv')))
for x in D[::12]:
    print(' '.join(f"{k}={float(x[k]):6.1f}" for k in ['t','ias','alpha','p','sr','stabL','stabR','sL_laoa','sR_laoa','flapL','flapR','rudL','rudR']))
