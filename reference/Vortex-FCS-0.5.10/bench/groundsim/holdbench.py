import sys; sys.path.insert(0,'.'); import gsim2
import numpy as np, gsindi as G
from gsim import Jet
dt=1/60
def hold_run(jet,V,T,minS,ind,gust,seed,Iz_told=None,b_told=None,K=15):
    # pedal released from t=0 with zero rate: the hold engages at once; gusts act; also a 2 deg heading kick at 2 s
    sim=G.Sim(jet,V,dt); c=G.Ctl(jet,T,minS,ind,K=K,Iz_told=Iz_told,b_told=b_told)
    rng=np.random.default_rng(seed); g=0; out=[]
    while sim.t<12-1e-9:
        cmd,brk=c.step(sim,0.0,dt)
        g += (-g/0.5 + gust*np.sqrt(2/0.5)*rng.standard_normal()/np.sqrt(dt))*dt
        sim.r += g*np.pi/180*dt
        if abs(sim.t-2.0)<dt/2: sim.r += 6*np.pi/180   # a 6 deg/s yaw kick (bump / gust edge)
        sim.step(cmd,brk); out.append((sim.t,sim.r*180/np.pi,sim.psi*180/np.pi))
    a=np.array(out); return a
for n,(p,T,minS) in G.JETS.items():
    print("==",n,"| hold: 6 deg/s yaw kick at 2 s + yaw gusts 3 deg/s^2 rms; heading peak after kick / heading rms 4-12 s / r rms")
    base=G.mk(p)
    for V in (10,25,50,70):
        for lab,pl,told in (("nominal",G.mk(p),{}),("plant heavy+stores (Iz +32%), INDI told nominal",G.mk(p,1.4,1.15),dict(Iz_told=base.Iz*1.4)),("plant CG aft -4%, INDI told nominal",G.mk(p,1,1,-0.04),dict(b_told=p['b']))):
            res=[]
            for ind in (False,True):
                pk=[];rm=[];rr=[]
                for sd in range(4):
                    a=hold_run(pl,V,T,minS,ind,3.0,sd,**(told if ind else {}))
                    i2=int(2/dt); i4=int(4/dt)
                    pk.append(np.max(np.abs(a[i2:i2+int(2/dt),2]-a[i2,2]))); rm.append(np.std(a[i4:,2])); rr.append(np.std(a[i4:,1]))
                res.append((np.mean(pk),np.mean(rm),np.mean(rr)))
            s,i=res
            print(f"  V {V:2d} {lab:48s} | kick peak deg PI {s[0]:.2f} INDI {i[0]:.2f} | psi rms PI {s[1]:.2f} INDI {i[1]:.2f} | r rms PI {s[2]:.2f} INDI {i[2]:.2f}")
