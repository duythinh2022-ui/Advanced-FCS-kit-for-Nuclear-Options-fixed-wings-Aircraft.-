# roughness only around the correction: window = where any 'on' tag had lsSlip active (-0.3/+0.8 s)
import pandas as pd,numpy as np,sys
tags=sys.argv[1:]; T=[78.3,124.4,129.8,613.4,858.5,950.1,1046.9,1049.8]; O=['-0.2','-0.1','0','0.1','0.15','0.2']
def hp(x,t,w=0.3):
    s=pd.Series(np.asarray(x),index=pd.to_datetime(np.asarray(t),unit='s')); return (s-s.rolling(f'{int(w*1000)}ms',center=True).mean())
R={k:{m:[] for m in ['pcj','pdj','pdmax','lsstep']} for k in tags}
for t in T:
    for o in O:
        D={k:pd.read_csv(f'rp/ls8/{k}_o{o}/{t}.csv') for k in tags}
        act=np.zeros(len(D[tags[0]]),bool)
        for k in tags:
            a=(D[k].lsSlip.abs()>0.01).values; act[:len(a)]|=a[:len(act)]
        if not act.any(): continue
        tt=D[tags[0]].t.values; ta,tb=tt[act].min()-0.3,tt[act].max()+0.8
        for k in tags:
            d=D[k]; m=(d.t>=ta)&(d.t<=tb); dt=np.gradient(d.t.values); pdot=np.gradient(d.p.values)/dt
            R[k]['pcj'].append(hp(d.pCmd,d.t)[m.values].std()); R[k]['pdj'].append(hp(pdot,d.t)[m.values].std())
            R[k]['pdmax'].append(np.abs(np.diff(pdot[m.values])).max()); R[k]['lsstep'].append(np.abs(np.diff(d.lsSlip.values[m.values])).max())
print(f'{"tag":9}{"pCmd jit":>10}{"pDot jit":>10}{"max dpDot/tick":>16}{"max lsSlip step":>17}   (n windows)')
for k in tags: print(f'{k:9}'+''.join(f'{np.mean(R[k][m]):{w}.2f}' for m,w in [('pcj',10),('pdj',10),('pdmax',16),('lsstep',17)])+f'   {len(R[k]["pcj"])}')
