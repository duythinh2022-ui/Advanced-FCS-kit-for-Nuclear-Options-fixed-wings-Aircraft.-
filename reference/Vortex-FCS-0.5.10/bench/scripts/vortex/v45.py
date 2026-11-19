import pandas as pd, numpy as np, sys
pd.set_option('display.width',280)
c=['t','gs','gsCmd','uFwd','vSide','vs','alt','phi','phiCmd','phiTrc','theta','thetaAssist','govLam','ratio','thrEng','swivel','aH','fwdCap','r','psiRate','yawLim','psi','postIn','yawIn']
f,t0,t1,step=sys.argv[1],float(sys.argv[2]),float(sys.argv[3]),int(sys.argv[4])
d=pd.read_csv(f); d['ratio']=d.thrustReq/(d.tMx*1e-3); d['gs']=d.gs/1.94384
c=[x for x in c if x in d.columns]
print(d[(d.t>t0)&(d.t<t1)][c].iloc[::step].round(2).to_string(index=False))
