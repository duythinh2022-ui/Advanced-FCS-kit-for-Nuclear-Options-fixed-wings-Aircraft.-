import pandas as pd,numpy as np,sys,warnings; warnings.filterwarnings('ignore')
R='/home/claude/su30/src/bench/runs/trig2/'
T={}
for l in open(R+'times.txt'):
    a=l.split(); T[(a[0],int(a[1]),int(a[2]))]=float(a[3])
rows=[]
for (tag,ias,a),tr in T.items():
    try: d=pd.read_csv(f'{R}{tag}_r{a}_{ias}.csv').copy()
    except Exception: continue
    al=np.radians(d.alpha); d['pS']=d.p*np.cos(al)+d.r*np.sin(al)
    w=d[(d.t>=tr)&(d.t<=tr+3)]
    if len(w)<100: continue
    rows.append(dict(tag=tag,ias=ias,a=a,aDip=(w.alpha.cummax()-w.alpha).max(),qMin=w.q.min(),vv=w.pS.abs().mean(),vvMinLate=w[w.t>tr+1].pS.abs().min(),bank=abs(np.trapezoid(w.pS,w.t)),bMax=w.beta.abs().max(),stall=(w.pS.abs()<5).sum()/60))
D=pd.DataFrame(rows); pd.set_option('display.width',200)
print(D.groupby('tag')[['aDip','qMin','vv','vvMinLate','bank','bMax','stall']].mean().round(1).to_string()); print('n',D.groupby('tag').size().to_dict())
print('by IAS'); print(D.pivot_table(index='ias',columns='tag',values=['aDip','bank'],aggfunc='mean').round(1).to_string())
print('by AoA'); print(D.pivot_table(index='a',columns='tag',values=['aDip','bank'],aggfunc='mean').round(1).to_string())
p=D.pivot_table(index=['ias','a'],columns='tag',values='bank'); print('bank cases where N9 beats B8:',int((p.N9c>p.B8c).sum()),'of',int(p.N9c.notna().sum()))
p=D.pivot_table(index=['ias','a'],columns='tag',values='aDip'); print('AoA-dip cases where N9 beats B8:',int((p.N9c<p.B8c).sum()),'of',int(p.N9c.notna().sum()))
