# compact time history: show.py <csv> <t0> <t1> <step_s> col1,col2,...
import sys, pandas as pd, numpy as np
f,t0,t1,st=sys.argv[1],float(sys.argv[2]),float(sys.argv[3]),float(sys.argv[4])
cols=sys.argv[5].split(',') if len(sys.argv)>5 else ['gs','vs','alt','theta','phi','thrEng','swivel','swivelTgt','hdotCmd','aDesV']
d=pd.read_csv(f); dt=d.t.diff().median(); k=max(1,int(round(st/dt)))
w=d[(d.t>=t0)&(d.t<=t1)].iloc[::k]
print(w[['t']+cols].round(2).to_string(index=False))
