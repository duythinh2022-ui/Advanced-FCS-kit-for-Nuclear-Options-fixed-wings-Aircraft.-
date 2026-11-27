import itertools,subprocess,os,sys,pandas as pd,numpy as np
from concurrent.futures import ThreadPoolExecutor
V={'L25S':['PitchLeadT=0.25','RollCaptStick=true'],'L25SP':['PitchLeadT=0.25','RollCaptStick=true','RollCaptPred=true'],'L30':['PitchLeadT=0.3'],'L15':['PitchLeadT=0.15'],'L25':['PitchLeadT=0.25'],'L35':['PitchLeadT=0.35'],'L50':['PitchLeadT=0.5'],'base':[],'S':['RollCaptStick=true'],'P':['RollCaptPred=true'],'SP':['RollCaptStick=true','RollCaptPred=true']}
if len(sys.argv)>1: V={k:V[k] for k in sys.argv[1].split(',')}
extra=[a for a in sys.argv[2:]]
cases=list(itertools.product([-55,-25,0],[400,500,600],[0.8,1.2,1.6]))
def run(c,vn):
    g,ias,rd=c; tag=f'hx_{vn}_{g}_{ias}_{rd}'+('_'+'_'.join(extra).replace('=','') if extra else '')
    f=f'runs/{tag}.csv'
    if not os.path.exists(f):
        subprocess.run(['dotnet','bin/bench.dll','one','AoANeg=-50','throttle=1','hold=5','post=0','rollFromPull=0','rollT=5',f'iasKmh={ias}',f'gamma0={g}','trimT=0.5','prof=0:1,5:1',f'rprof=0:0,{rd-0.05}:0,{rd}:1,5:1',f'csv={f}']+V[vn]+extra,capture_output=True)
    d=pd.read_csv(f); d['t']=d.t+5; w=d[d.t>=rd]
    tv=np.sign(np.round(w.tvc/15)); flips=int(((tv.diff()!=0)&(tv!=0)).sum())
    return dict(v=vn,g=g,ias=ias,rd=rd,over=w.alpha.max()-65,qOver=(w.q-w.qCmd).max(),hitch=(w.qCmd-w.q)[w.alpha>40].max() if (w.alpha>40).any() else 0,tvcFlips=flips,a1=d.iloc[(d.t-1.5).abs().argmin()].alpha,bank=w.phi.iloc[-1],bmax=w.beta.abs().max())
jobs=[(c,v) for v in V for c in cases]
with ThreadPoolExecutor(2) as ex: R=pd.DataFrame(list(ex.map(lambda j: run(*j),jobs)))
R.to_csv('hitch_res.csv',index=False)
pd.set_option('display.width',250)
print(R.groupby('v')[['over','qOver','hitch','tvcFlips','bmax']].agg(['mean','max']).round(2))
