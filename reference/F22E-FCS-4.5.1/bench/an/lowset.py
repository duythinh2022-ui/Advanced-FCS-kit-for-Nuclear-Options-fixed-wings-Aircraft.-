import subprocess, sys, os, itertools, numpy as np, pandas as pd
# usage: lowset.py <tag> [Name=value ...]
tag=sys.argv[1]; extra=sys.argv[2:]
R=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
out=os.path.join(R,'bench','out','lowset',tag); os.makedirs(out,exist_ok=True)
rows=[]
for kind in ['push','pull']:
    for thr in [0.6,1.0]:
      for T in [2.5,3.0,3.5,4.0]:
        for rs in [0.0,0.2,0.5]:
            ias=thr
            if kind=='push': prof=f"0:1,{T}:1,{T+0.08}:-1,{T+5}:-1"
            else: prof=f"0:-1,{T}:-1,{T+0.08}:1,{T+5}:1"
            rprof=f"0:{rs},{T}:{rs},{T+0.08}:0,{T+5}:0"
            csv=f"{out}/{kind}_{thr}_{T}_{rs}.csv"
            subprocess.run(['bash',f'{R}/bench.sh','one','iasKmh=250','alt=1500','trimT=1','gamma0=60',f'throttle={thr}','post=8',f'prof={prof}',f'rprof={rprof}',f'csv={csv}']+extra,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
            d=pd.read_csv(csv); ts=T
            sgn=1 if kind=='push' else -1
            w=d[d.t>=ts]; i0=w.index[0]; r0=d.loc[i0]
            a=np.radians(r0.alpha); ps=abs(r0.p*np.cos(a)+r0.r*np.sin(a))
            al=w.alpha.values; t=w.t.values
            # time to cross 20 deg toward the far side / to reach 0
            if kind=='push': hit=np.argmax(al<20) if (al<20).any() else -1
            else: hit=np.argmax(al>0) if (al>0).any() else -1
            tt=t[hit]-ts if hit>=0 else np.nan
            ww=w[w.t<ts+(tt if tt==tt else 3)+0.3]
            qc=-sgn*ww.qCmd.values  # positive = toward the stick
            peak=np.maximum.accumulate(qc); retreat=(peak-qc).max()
            tv=ww.tvc.values; flips=int(np.sum(np.abs(np.diff(np.sign(np.where(np.abs(tv)>6,tv,0))))>1))
            qmin=(-sgn*ww.q.values)[int(0.4*60):].min() if len(ww)>30 else np.nan
            rows.append((kind,f'{thr}/{T}',rs,r0.ias/1.852,r0.alpha,ps,tt,retreat,flips,w.beta.abs().max()))
R=pd.DataFrame(rows,columns=['kind','thr/T','roll','ktPush','a0','pS0','t_target','qcRetreat','tvcFlips','betaMax'])
R.to_csv(f'{out}/summary.csv',index=False)
pd.set_option('display.width',200)
print(R.round(2).to_string(index=False))
