# regression scenarios for the stock-jet benches (ifly). usage: BENCH_DIR=... BENCH_DATA=... sreg.py <tag> [Name=value ...]
import subprocess, os, sys, numpy as np, pandas as pd
from concurrent.futures import ThreadPoolExecutor
S=os.environ.get('AN_OUT','/home/claude/work/anout'); B=os.environ['BENCH_DIR']
tag=sys.argv[1]; sets=sys.argv[2:]; out=S+'/reg/'+tag; os.makedirs(out,exist_ok=True)
ALT=os.environ.get('SW_ALT','1500')
CASES=[]
for ias in [250,320,400]: CASES.append((f'pull-then-roll {ias}',ias,"0:0,0.05:1,6:1","0:0,0.3:0,0.35:1,2.5:1,2.55:0,6:0",None,(0,3.0)))
CASES.append(('fullaft+roll 250',250,"0:0,0.05:1,6:1","0:0,0.05:1,3:1,3.05:0,6:0",None,(0,3.0)))
for ias in [320,500]: CASES.append((f'fullaft+roll+rev3s {ias}',ias,"0:0,0.05:1,6:1","0:0,0.05:1,3:1,3.05:-1,4.2:-1,4.25:0,6:0",None,(0,4.5)))
CASES.append(('roll-then-pull-rev 400',400,"0:0,0.5:0,0.55:1,6:1","0:0,0.05:1,1.5:1,1.55:-1,2.6:-1,2.65:0,6:0",None,(0,3.5)))
CASES.append(('held0.4+roll 350',350,"0:0,0.05:0.4,6:0.4","0:0,1.2:0,1.25:1,3:1,3.05:0,6:0",None,(1.2,3.5)))
CASES.append(('65AoA roll+yaw release 300',300,"0:0,0.05:1,8:1","0:0,2.5:0,2.55:1,5.5:1,5.55:0,8:0","0:0,2.5:0,2.55:1,5.5:1,5.55:0,8:0",(5.5,8.0)))
CASES.append(('65AoA roll release 300',300,"0:0,0.05:1,8:1","0:0,2.5:0,2.55:1,5.5:1,5.55:0,8:0",None,(5.5,8.0)))
for ias in [200,300]: CASES.append((f'push+roll {ias}',ias,"0:0,0.05:-1,4:-1","0:0,0.05:1,3:1,3.05:0,6:0",None,(0,3.5)))
for ias in [350,450]: CASES.append((f'pull-then-pushover+roll {ias}',ias,"0:0,0.05:0.6,0.8:0.6,0.9:-1,4:-1","0:0,0.3:0,0.35:1,3.5:1,3.55:0,6:0",None,(0.8,3.5)))
for ias in [350,450]: CASES.append((f'push-then-pullthrough+roll {ias}',ias,"0:0,0.05:-1,1.4:-1,1.5:1,5:1","0:0,0.3:0,0.35:1,4:1,4.05:0,6:0",None,(1.4,4.0)))
CASES.append(('pullthrough, roll released at zero 350',350,"0:0,0.05:-1,1.4:-1,1.5:1,5:1","0:0,0.3:0,0.35:1,1.9:1,1.95:0,6:0",None,(1.4,4.0)))
CASES.append(('1g roll rev 500',500,"0:0,8:0","0:0,0.05:1,1.0:1,1.05:-1,2.0:-1,2.05:0,6:0",None,(0,3.0)))
CASES.append(('push+roll -55 corkscrew 170',170,"0:0,0.05:-1,8:-1","0:0,0.05:1,6:1,6.05:0,8:0",None,(0,6.0)))
CASES.append(('roll-then-push -55 200',200,"0:0,0.8:0,0.85:-1,8:-1","0:0,0.05:1,6:1,6.05:0,8:0",None,(0.8,6.0)))
def run(i):
    name,ias,prof,rprof,yprof,win=CASES[i]; csv=f"{out}/c{i}.csv"
    if os.path.exists(csv): return csv
    T='8' if ('65AoA' in name or 'corkscrew' in name or '-55' in name) else '7'
    cmd=['dotnet',B+'/bin/bench.dll','ifly',f'ias={ias}',f'alt={ALT}','thr=1',f'T={T}',f'pitch={prof}',f'roll={rprof}',f'fcslog={csv}']+sets
    if yprof: cmd.append(f'yaw={yprof}')
    r=subprocess.run(cmd,cwd=B,env=dict(os.environ),capture_output=True,text=True)
    if not os.path.exists(csv): print("FAILED",name,r.stderr[-400:])
    return csv
with ThreadPoolExecutor(4) as ex: files=list(ex.map(run,range(len(CASES))))
rows=[]
for (name,ias,prof,rprof,yprof,win),f in zip(CASES,files):
    d=pd.read_csv(f); w=d[(d.t>=win[0])&(d.t<=win[1])]
    a2=d[d.t>=2.0].alpha.iloc[0] if len(d[d.t>=2.0]) else np.nan
    rows.append(dict(case=name,betaPk=round(w.beta.abs().max(),1),betaQ95=round(w.beta.abs().quantile(.95),1),alpha2s=round(a2,1),pMax=round(w.p.abs().max()),pMean=round(w.p.abs().mean()),alphaMax=round(w.alpha.max(),1),alphaMin=round(w.alpha.min(),1),rMax=round(w.r.abs().max()),ysp=round(w.yawSatPitch.max(),1) if 'yawSatPitch' in d else 0,pace=round(w.paceHold.abs().max(),1) if 'paceHold' in d else 0,ysat=round(w.yawSat.max(),2)))
r=pd.DataFrame(rows); r.to_csv(out+'/summary.csv',index=False); pd.set_option('display.width',250)
print(tag,' '.join(sets)); print(r.to_string(index=False))
