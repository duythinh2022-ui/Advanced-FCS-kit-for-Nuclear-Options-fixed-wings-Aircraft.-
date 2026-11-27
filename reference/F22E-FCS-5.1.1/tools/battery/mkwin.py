# cut the 10:34 log into one small csv per no-TVC approach past 40 deg (AoAG mode); windows.json lists them
import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *; import sys  # kit 5.0: paths relative to this tree
import pandas as pd, numpy as np, json, sys
LOG=sys.argv[1] if len(sys.argv)>1 else LOGDIR+"/F22E_FCS_20261126_103449.csv"
d=pd.read_csv(LOG)
a=d.alpha.values; t=d.t.values; m=d['mode'].values; tv=d.tvcNow.values
W=[];i=1;n=len(a)
while i<n:
    if a[i-1]<40<=a[i] and m[i]==3:
        j=i;pk=i
        while j<n and m[j]==3:
            if a[j]>a[pk]: pk=j
            if a[j]<a[pk]-6 or a[j]<34: break
            j+=1
        k=i
        while k>0 and a[k]>30 and t[i]-t[k]<3: k-=1
        if np.abs(tv[k:pk+1]).max()<0.5:
            t0=t[k]; t1=min(t[pk],t0+5)+1.0
            W.append(dict(id=len(W),t0=round(float(t0),3),t1=round(float(t1),3),tpk=float(t[pk]),pk=float(a[pk]),t40=float(t[i])))
        i=j
    i+=1
lines=open(LOG).read().split('\n'); hdr=lines[0]
tt=np.array([float(l.split(',',1)[0]) if l else 1e9 for l in lines[1:]])
for w in W:
    sel=np.where((tt>=w['t0']-0.7)&(tt<=w['t1']+0.7))[0]
    open(f"{OV}/win/w{w['id']:02d}.csv",'w').write(hdr+'\n'+'\n'.join(lines[1+s] for s in sel)+'\n')
json.dump(W,open(OV+'/windows.json','w'),indent=0)
print(len(W), sum(w['pk']>55.5 for w in W))
