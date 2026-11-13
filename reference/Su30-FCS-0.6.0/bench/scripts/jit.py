import pandas as pd,numpy as np,sys
tags=sys.argv[1:]; T=[78.3,124.4,129.8,613.4,858.5,950.1,1046.9,1049.8]; O=['-0.2','-0.1','0','0.1','0.15','0.2']
def hp(x,t,w=0.3):
    s=pd.Series(x.values,index=pd.to_datetime(t.values,unit='s')); return (s-s.rolling(f'{int(w*1000)}ms',center=True).mean()).std()
cols=['pCmd','rCmd','tvcdNow','rudder_L_now']
print('tag      '+''.join(f'{c:>12}' for c in cols+['pDot','rDot','pDotMax','lsOnFrac']))
for tag in tags:
    acc={c:[] for c in cols+['pDot','rDot','pDotMax','on']}
    for t in T:
        for o in O:
            d=pd.read_csv(f'rp/ls8/{tag}_o{o}/{t}.csv'); w=d[d.t>=t-2.5]
            for c in cols: acc[c].append(hp(w[c],w.t))
            dt=np.gradient(w.t.values); pd_=np.gradient(w.p.values)/dt; rd=np.gradient(w.r.values)/dt
            acc['pDot'].append(hp(pd.Series(pd_),w.t)); acc['rDot'].append(hp(pd.Series(rd),w.t)); acc['pDotMax'].append(np.abs(pd_).max())
            acc['on'].append((w.lsSlip.abs()>0.01).mean())
    print(f'{tag:8} '+''.join(f'{np.mean(acc[c]):12.3f}' for c in cols+['pDot','rDot','pDotMax','on']))
