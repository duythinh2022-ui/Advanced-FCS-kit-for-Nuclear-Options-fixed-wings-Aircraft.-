import sys, glob, pandas as pd, numpy as np
sreg=sys.argv[1]; base=sys.argv[2]; new=sys.argv[3]
# arm from the new runs: betaRoot - beta = arm * r / tas (deg, deg/s, m/s)
X=[];Y=[]
for f in glob.glob(new+'/c*.csv'):
    d=pd.read_csv(f,usecols=['beta','betaRoot','r','tas']); d=d[d.tas>30]
    X.append(d.r/d.tas); Y.append(d.betaRoot-d.beta)
X=pd.concat(X); Y=pd.concat(Y); arm=float((X*Y).sum()/(X*X).sum()); res=Y-arm*X
print(f"arm fit: {arm:+.3f} m (residual rms {res.std():.3f} deg)")
src=open(sreg).read(); cs=src[src.index('CASES=[]'):src.index('def run(i)')]; CASES=[]; ns={'CASES':CASES}; exec(cs.replace('CASES=[]',''),ns)
rows=[]
for i,(name,ias,prof,rprof,yprof,win) in enumerate(CASES):
    b=pd.read_csv(f"{base}/c{i}.csv"); n=pd.read_csv(f"{new}/c{i}.csv")
    wb=b[(b.t>=win[0])&(b.t<=win[1])]; wn=n[(n.t>=win[0])&(n.t<=win[1])]
    bcg=(wb.beta-arm*wb.r/wb.tas.clip(lower=30))
    rows.append(dict(case=name, base_atCg=round(bcg.abs().max(),1), cg_atCg=round(wn.beta.abs().max(),1)))
r=pd.DataFrame(rows); d=r.cg_atCg-r.base_atCg; r['delta']=d.round(1); print(r.to_string(index=False))
print(f"CG-sideslip peak delta: mean {d.mean():+.2f}, worst {d.max():+.1f}, best {d.min():+.1f}; worse >0.5: {(d>0.5).sum()} of {len(d)}")
