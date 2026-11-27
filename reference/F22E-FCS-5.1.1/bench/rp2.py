# rolling-start / combined pull cases at low-moderate IAS; prints max sideslip and AoA build
import itertools,subprocess,os,sys,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
tag=sys.argv[1]; extra=sys.argv[2:]
cases=[]
for ias,rs,pd_ in itertools.product([220,290,380,460],[1.0,0.5],[0.0,0.4,1.0]):
    # roll stick rs (left) from t=0, full aft at pd_ s (0 = together); roll goes full when pull starts
    cases.append((ias,rs,pd_))
def run(c):
    ias,rs,pd_=c; f=f'runs/{tag}_{ias}_{rs}_{pd_}.csv'
    if not os.path.exists(f):
        prof=f'0:0,{max(pd_-0.01,0):.2f}:0,{pd_+0.0:.2f}:1,5:1' if pd_>0 else '0:1,5:1'
        rprof=f'0:-{rs},{max(pd_-0.01,0):.2f}:-{rs},{pd_+0.05:.2f}:-1,5:-1' if pd_>0 else '0:-1,5:-1'
        subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50','throttle=1','trimT=2','hold=5','post=0','rollFromPull=0','rollT=5',f'iasKmh={ias}',f'prof={prof}',f'rprof={rprof}',f'csv={f}']+extra,capture_output=True)
    d=pd.read_csv(f); d['t']=d.t+5; w=d[d.t>=0]
    t20=w.t[w.alpha>20]; t40=w.t[w.alpha>40]
    return dict(ias=ias,rs=rs,pd=pd_,bmax=w.beta.abs().max(),t20=(t20.iloc[0]-pd_) if len(t20) else np.nan,t40=(t40.iloc[0]-pd_) if len(t40) else np.nan,amax=w.alpha.max(),bank2=w.phi[(w.t-(pd_+2)).abs().idxmin()],sweep=0,a15=w.alpha[(w.t-(pd_+1.5)).abs().idxmin()],pmin=w.p[(w.t>pd_)&(w.t<pd_+2)].abs().min(),pmean=w.p[(w.t>pd_)&(w.t<pd_+2)].abs().mean())
with ThreadPoolExecutor(4) as ex: R=pd.DataFrame(list(ex.map(run,cases)))
R.to_csv(f'{tag}_rp.csv',index=False)
pd.set_option('display.width',200)
print(R.round(2).to_string()); print('%s bmax mean %.1f worst %.1f  t20 med %.2f t40 med %.2f a1.5 mean %.1f |p| mean %.0f'%(tag,R.bmax.mean(),R.bmax.max(),R.t20.median(),R.t40.median(),R.a15.mean(),R.pmean.mean()))
