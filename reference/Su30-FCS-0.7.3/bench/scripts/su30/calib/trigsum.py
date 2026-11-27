import pandas as pd,numpy as np,sys,glob,warnings; warnings.filterwarnings('ignore')
R='/home/claude/su30/src/bench/runs/trig/'
T={}
for l in open(R+'times.txt'):
    a=l.split(); T[(a[0],int(a[1]),int(a[2]))]=float(a[3])
rows=[]
tags=sys.argv[1].split(',')
for ias in [430,520,600]:
  for a in [30,45,58]:
    for tag in tags:
      if (tag,ias,a) not in T: continue
      tr=T[(tag,ias,a)]; d=pd.read_csv(f'{R}{tag}_r{a}_{ias}.csv').copy()
      al=np.radians(d.alpha); d['pS']=d.p*np.cos(al)+d.r*np.sin(al)
      w=d[(d.t>=tr)&(d.t<=tr+3)]
      am=w.alpha.cummax(); 
      rows.append(dict(case=f'{ias}/{a}',tag=tag,tRoll=tr,aDip=(am-w.alpha).max(),qMin=w.q.min(),vv=w.pS.abs().mean(),vvMin_after1=w[w.t>tr+1].pS.abs().min(),bank3s=abs(np.trapezoid(w.pS,w.t)),bMax=w.beta.abs().max(),nozS=((w.nozL+w.nozR)/2).abs().mean(),aEnd=w.alpha.iloc[-1],ias3=w.ias.iloc[-1]*3.6))
pd.set_option('display.width',200); print(pd.DataFrame(rows).round(1).to_string(index=False))
