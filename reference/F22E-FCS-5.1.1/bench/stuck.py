import sys,pandas as pd,numpy as np
for t in sys.argv[1:]:
  w=pd.read_csv(f'runs/rep_{t}.csv'); e=w[w.t>w.t.iloc[-1]-12]
  print(t,'last 12 s: |stabL-stabR|/2 mean %.1f  |flapL| mean %.1f |ailL| %.1f |rudL-rudR| %.1f  tvc %.1f  alpha %.1f'%(((e.stabL-e.stabR)/2).abs().mean(),e.flapL.abs().mean(),e.ailL.abs().mean(),(e.rudL-e.rudR).abs().mean(),e.tvc.abs().mean(),e.alpha.mean()))
