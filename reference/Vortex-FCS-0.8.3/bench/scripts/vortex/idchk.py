# identity of the bench output: <dirA> vs <dirB>, every column they share, per case file
import pandas as pd, numpy as np, sys, glob, os
a,b=sys.argv[1],sys.argv[2]; allok=True
for f in sorted(glob.glob(a+'/*.csv')):
    g=b+'/'+os.path.basename(f)
    if not os.path.exists(g): print("  missing",g); allok=False; continue
    x=pd.read_csv(f); y=pd.read_csv(g); com=[c for c in x.columns if c in y.columns]
    if len(x)!=len(y): print(f"  {os.path.basename(f)}: rows {len(x)} vs {len(y)}"); allok=False; continue
    d=np.abs(x[com].values-y[com].values); bad=[c for c,m in zip(com,np.nanmax(d,axis=0)) if m>0]
    print(f"  {os.path.basename(f):8s} rows {len(x):6d} cols {len(com)}: "+("identical" if not bad else f"DIFF in {len(bad)} cols, max {np.nanmax(d):.4g} ({bad[:6]})")); allok&=not bad
print("ALL IDENTICAL" if allok else "NOT IDENTICAL")
