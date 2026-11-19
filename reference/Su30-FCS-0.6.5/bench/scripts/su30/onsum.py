import pandas as pd, numpy as np, sys, warnings; warnings.filterwarnings('ignore')
R='/home/claude/su30/v10/src/bench/runs/'
rows=[]
for tag in sys.argv[1].split(','):
  for c in ['on','o6']:
    for ias in [350,450,550,650,750,850]:
      try: d=pd.read_csv(f'{R}{tag}_{c}_{ias}.csv')
      except: continue
      x=d[d.t>0.4].reset_index(drop=True)
      # window: from pull start until AoA 40 or 2.5 s, whichever first
      end=x.index[(x.alpha>=40)|(x.t>2.9)]
      e=end[0] if len(end) else len(x)
      w=x.iloc[:e]
      qc=w.qCmd.values; q=w.q.values
      dipC=(np.maximum.accumulate(qc)-qc).max(); dipQ=(np.maximum.accumulate(q)-q)[np.argmax(q>5):].max() if (q>5).any() else 0
      rows.append(dict(tag=tag,case=f'{c}{ias}',qCmdDip=dipC,qDip=dipQ,qCmdMax=qc.max(),nzMax=x.nz.max(),t40=(x[x.alpha>=40].t.min()-0.4),aMax=x.alpha.max()))
pd.set_option('display.width',200); print(pd.DataFrame(rows).round(2).to_string(index=False))
