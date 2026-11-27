# compare two FCS csvs column by column (common columns; timing columns skipped). usage: csvsame.py a.csv b.csv
import sys, pandas as pd, numpy as np
a=pd.read_csv(sys.argv[1]); b=pd.read_csv(sys.argv[2])
skip=lambda c: c.endswith('Us') or c.startswith('sg') or c in ('heapB','gcRan','estThr','fcsUs')
cols=[c for c in a.columns if c in b.columns and not skip(c)]
if len(a)!=len(b): print('row count differs',len(a),len(b)); sys.exit()
bad=[]
for c in cols:
    x=pd.to_numeric(a[c],errors='coerce').values; y=pd.to_numeric(b[c],errors='coerce').values
    d=np.nanmax(np.abs(x-y)) if len(x) else 0
    if d>0: bad.append((c,d))
print(f"{len(cols)} common columns, {len(a)} rows:", 'IDENTICAL' if not bad else f"{len(bad)} differ, worst {sorted(bad,key=lambda z:-z[1])[:5]}", '| only in b:', [c for c in b.columns if c not in a.columns])
