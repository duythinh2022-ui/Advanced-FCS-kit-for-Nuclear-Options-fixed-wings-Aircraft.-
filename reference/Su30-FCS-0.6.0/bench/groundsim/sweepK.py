import sys; sys.path.insert(0,'.'); import gsim2
import numpy as np, gsindi as G
dt=1/60
for n,(p,T,minS) in G.JETS.items():
    print("==",n)
    for K,tau,sm in ((5,0.04,1.2),(10,0.04,1.2),(15,0.04,1.2),(20,0.04,1.2),(15,0.02,1.2),(15,0.04,2.0),(15,0.04,3.0)):
        G.Ctl.GsSatMargin=sm
        row=[]
        for V in (4,10,20,25):
            for pa in (1.0,0.5):
                st=[]
                for ind in (False,True):
                    a=G.run(G.mk(p),V,lambda t,pa=pa: pa if t<3 else 0.0,T,minS,ind,Tend=8,K=K,tau=tau)
                    r=a[int(3/dt):,1]
                    ist=next((k for k,x in enumerate(r) if abs(x)<1.0),None)
                    osc=np.max(-np.sign(r[0])*r[ist:]) if ist else 9
                    st.append((ist*dt if ist else 9, osc))
                row.append(f"{st[1][0]-st[0][0]:+.2f}({st[1][1]:.1f})")
        G.Ctl.GsSatMargin=1.2
        print(f" K {K:2d} tau {tau} satM {sm}: INDI minus 2.4.13 stop time s (swing-back deg/s), V4 f/h V10 f/h V20 f/h V25 f/h:", " ".join(row))
