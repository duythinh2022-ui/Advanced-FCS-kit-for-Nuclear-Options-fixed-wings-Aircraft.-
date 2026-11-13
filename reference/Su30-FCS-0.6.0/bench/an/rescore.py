# rescore the reg suite of a CG-air-data run at the root (betaRoot/alphaRoot), next to the baseline (root by construction)
import sys, pandas as pd, numpy as np, importlib.util, os
sreg=sys.argv[1]; base=sys.argv[2]; new=sys.argv[3]
src=open(sreg).read(); cases_src=src[src.index('CASES=[]'):src.index('def run(i)')]
CASES=[]; ns={'CASES':CASES}; exec(cases_src.replace('CASES=[]',''),ns)
rows=[]
for i,(name,ias,prof,rprof,yprof,win) in enumerate(CASES):
    b=pd.read_csv(f"{base}/c{i}.csv"); n=pd.read_csv(f"{new}/c{i}.csv")
    wb=b[(b.t>=win[0])&(b.t<=win[1])]; wn=n[(n.t>=win[0])&(n.t<=win[1])]
    rows.append(dict(case=name, bPk_base=round(wb.beta.abs().max(),1), bPk_cgAtRoot=round(wn.betaRoot.abs().max(),1), bPk_cgAtCg=round(wn.beta.abs().max(),1),
      aMax_base=round(wb.alpha.max(),1), aMax_cgAtRoot=round(wn.alphaRoot.max(),1), pMax_base=round(wb.p.abs().max()), pMax_cg=round(wn.p.abs().max()),
      pMean_base=round(wb.p.abs().mean()), pMean_cg=round(wn.p.abs().mean())))
r=pd.DataFrame(rows); pd.set_option('display.width',250); print(r.to_string(index=False))
d=r.bPk_cgAtRoot-r.bPk_base; print(f"root-sideslip peak delta (cg - base): mean {d.mean():+.2f}, worst {d.max():+.1f}, best {d.min():+.1f}; cases worse >0.5: {(d>0.5).sum()} of {len(d)}")
