# F-22E held loaded roll on the bench: surface travel and governor activity. usage: fhold.py <tree> <tag> [sets]
import subprocess, os, sys, numpy as np, pandas as pd
from scipy.signal import butter, filtfilt
tree=os.path.abspath(sys.argv[1]); tag=sys.argv[2]; sets=sys.argv[3:]; B=tree+'/bench'; O='/home/claude/work/runs'
env=dict(os.environ,BENCH_DATA=B+'/data/airframe_cal.json')
for ias,pull,tvc in [(300,0.45,0),(460,0.45,0),(300,0.45,1),(460,0.45,1),(400,1.0,0),(400,1.0,1),(550,0.6,0)]:
    csv=f"{O}/fh_{tag}_{ias}_{pull}_{tvc}.csv"
    cmd=['dotnet',B+'/bin/bench.dll','one',f'iasKmh={ias}','alt=1500','trimT=1','pull=0','hold=0','roll=0','post=12','throttle=1',f'prof=0:0,0.05:{pull},12:{pull}','rprof=0:0,3:0,3.05:1,12:1',f'csv={csv}']+(['DisableTVC=true'] if tvc else [])+sets
    r=subprocess.run(cmd,cwd=B,env=env,capture_output=True,text=True)
    d=pd.read_csv(csv); d=d[(d.t>=3.6)&(d.t<=11.5)].reset_index(drop=True); dt=d.t.diff().median()
    b,a=butter(2,[0.1*2*dt,min(0.99,10*2*dt)],'band'); bp=lambda x: filtfilt(b,a,np.asarray(x,float)).std()
    cs=[c for c in ['stabL','stabR','rudL','rudR','ailL','ailR','flapL','flapR'] if c in d]
    v=[np.abs(np.gradient(d[c].values,d.t.values)) for c in cs]
    tg='pSTgt' if 'pSTgt' in d else None
    print(f"{tag:6s} ias {ias} pull {pull} tvc {'off' if tvc else 'on '}: travel {np.mean([x.mean() for x in v]):5.1f} deg/s (max {np.max([x.mean() for x in v]):5.1f}) atLimit {np.mean([100*(x>=0.9*98).mean() for x in v]):4.1f}% band {np.mean([bp(d[c]) for c in cs]):4.2f} | tgt band {bp(d[tg]) if tg else float('nan'):5.2f} | p {d.p.abs().mean():5.1f} aoa {d.alpha.mean():4.1f} |b|max {d.beta.abs().max():4.1f} ias_end {d.ias.iloc[-1]:4.0f}")
