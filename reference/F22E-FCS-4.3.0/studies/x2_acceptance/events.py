# planned push-over onsets (rising edge of ppOn) in the staged logs -> events.json
import pandas as pd,glob,json,os
E={}
for f in sorted(glob.glob('/home/claude/w/acc/logs/F22E_FCS_*.csv')):
    h=open(f).readline().strip().split(',')
    if 'ppOn' not in h: continue
    d=pd.read_csv(f,usecols=['t','ppOn','alpha','ias'],on_bad_lines='skip')
    d['ppOn']=pd.to_numeric(d.ppOn,errors='coerce').fillna(0)
    on=d.ppOn.values>0.5; ts=[]
    for i in range(1,len(d)):
        if on[i] and not on[i-1]:
            t=float(d.t.iloc[i]); 
            if not ts or t-ts[-1]>1.0: ts.append(t)
    E[os.path.basename(f)]=ts; print(os.path.basename(f),len(ts))
json.dump(E,open('events.json','w'))
print(sum(len(v) for k,v in E.items() if '20261107' not in k),'in 5-6 Nov;',sum(len(v) for k,v in E.items() if '20261107' in k),'in 7 Nov')
