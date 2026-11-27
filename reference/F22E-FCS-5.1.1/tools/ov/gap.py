import pandas as pd, numpy as np, json, sys
tag=sys.argv[1]; W=json.load(open("windows.json")); S=[]
for w in W:
    d=pd.read_csv(f"out/{tag}/w{w['id']:02d}.csv").copy()
    q=pd.Series(d.q_b.values).rolling(5,center=True,min_periods=1).mean().values
    d['ach']=-np.gradient(q,d.t.values); d['hd']=d.dnStab+d.dnFlap+d.dnRud+d.dnOther
    S.append(d[(d.alpha_b>40)].assign(id=w['id']))
S=pd.concat(S); E=S[(S.hd<2)&(S.unmetY_b<-2)].copy()
E['band']=pd.cut(E.alpha_b,[45,50,53,56,62]); E['roll']=np.where(E.coupNow.abs()>3,'coup>3','coup<3')
E['gap']=E.dnFullHere-E.coupNow-E.ach
print(tag, len(E))
print(E.groupby(['roll','band'],observed=True).agg(n=('ach','size'),ach=('ach','median'),model=('dnFullHere','median'),coup=('coupNow','median'),gap=('gap','median'),pk=('alpha_b','max')).round(1).to_string())
