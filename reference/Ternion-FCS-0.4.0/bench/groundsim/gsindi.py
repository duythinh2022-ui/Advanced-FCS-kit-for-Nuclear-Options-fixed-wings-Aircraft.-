#!/usr/bin/env python3
# Bench: 2.4.13 ground steering (stock law + release braking via stock law + PI line hold) vs the INDI nose wheel
# (release braking and line hold), on gsim's plant (NO LandingGear tyre/castor/slew). Plant inertia/CG can differ
# from what the INDI is told (load robustness). Optional yaw-moment gusts.
import math, sys
import numpy as np
sys.path.insert(0, ".")
from gsim import Jet, Sim, tyre
D2R, R2D = math.pi/180, 180/math.pi

class Ctl:
    GsKp0, GsKpMin, GsKpMax, GsKi, GsLimK, GsSatMargin = 15.0, 0.15, 3.0, 1.0, 300.0, 1.2
    GsYawAccel, GsKpsi, GsHoldRate = 20.0, 1.0, 4.0
    def __init__(s, jet, T, minS, indi, K=15.0, tau=0.04, Iz_told=None, b_told=None):
        s.j=jet; s.T=T; s.minS=minS; s.indi=indi; s.K=K; s.tau=tau
        s.Iz=Iz_told if Iz_told else jet.Iz
        bf = b_told if b_told is not None else jet.b/jet.L
        s.Nn_told = jet.m*9.81*bf          # nose load the controller believes (in game: read from the gear each tick)
        s.a_told = jet.L*(1-bf)
        s.hold=False; s.brakeT=0; s.psiRef=0; s.rcmd=0; s.int=0
        s.rprev=None; s.rdf=0; s.Ff=0
    def stock_y(s, ped, r, V):
        if V < s.minS: return ped
        return max(-1,min(1,s.T*(ped - r)))
    def step(s, sim, ped, dt):
        j=s.j; V=sim.V; r=sim.r*R2D
        rd = 0.0 if s.rprev is None else (r - s.rprev)/dt
        s.rprev = r
        a = dt/(s.tau+dt)
        s.rdf += (rd - s.rdf)*a
        # nose contact geometry / tyre now (what the game exposes: wheel angle, contact velocity, N, mu)
        vny = sim.vy + sim.r*j.a
        alpha_c = math.atan2(vny, V)*R2D
        k = 0.2 + 0.01*V
        Fnow = max(-1,min(1,k*(sim.delta - alpha_c)))*j.mu_n*s.Nn_told
        s.Ff += (Fnow - s.Ff)*a
        if abs(ped) > 0.05 or V < 1.5:
            s.hold=False; s.brakeT=0
            y = s.stock_y(ped, sim.r, V); s.rcmd = r
            return y, y
        if not s.hold:
            s.brakeT += dt
            if abs(r) < 0.7 or s.brakeT > 2.0:
                s.hold=True; s.psiRef=sim.psi*R2D
        ys = s.stock_y(0.0, sim.r, V)
        if not s.hold:
            s.rcmd = r
            if not s.indi: return ys, ys
            rc = 0.0; rcdot = 0.0
        else:
            e = s.psiRef - sim.psi*R2D
            rt = max(-s.GsHoldRate,min(s.GsHoldRate,s.GsKpsi*e))
            old = s.rcmd
            s.rcmd += max(-s.GsYawAccel*dt,min(s.GsYawAccel*dt, rt - s.rcmd))
            rc = s.rcmd; rcdot = (rc-old)/dt
        if s.indi:
            nu = s.K*(rc - r) + rcdot
            Fdes = s.Ff + (nu - s.rdf)*D2R*s.Iz/s.a_told
            mN = j.mu_n*s.Nn_told
            sdes = max(-s.GsSatMargin/k, min(s.GsSatMargin/k, Fdes/(mN*k)))
            dcmd = max(-j.lock, min(j.lock, alpha_c + sdes))
            blend = min(1,max(0,(V-1.5)/1.5))
            nws = (1-blend)*ped*j.lock + blend*dcmd
            return nws/j.lock, ys if not s.hold else 0.0
        # 2.4.13 PI line hold
        err = rc - r
        ff = math.atan(rc*D2R*j.L/max(V,1))*R2D
        kp = max(s.GsKpMin,min(s.GsKpMax,s.GsKp0/max(V,3)))
        lim = min(j.lock, s.GsLimK/max(V,1))
        sat = s.GsSatMargin/(0.2+0.01*V)
        s.int = max(-sat,min(sat,s.int + s.GsKi*kp*err*dt))
        fb = max(-sat,min(sat,kp*err+s.int))
        blend = min(1,max(0,(V-1.5)/1.5))
        nws = (1-blend)*ped*j.lock + blend*max(-lim,min(lim,ff+fb))
        return nws/j.lock, 0.0

def run(jet, V, pf, T, minS, indi, Tend=7.0, dt=1/60, gust=0.0, seed=1, **kw):
    sim=Sim(jet,V,dt); c=Ctl(jet,T,minS,indi,**kw); rng=np.random.default_rng(seed)
    out=[]; g=0.0
    while sim.t < Tend-1e-9:
        p=pf(sim.t)
        cmd, brk = c.step(sim, p, dt)
        if gust>0:   # first-order filtered yaw-moment noise, gust = rms yaw accel deg/s^2
            g += (-g/0.5 + gust*math.sqrt(2/0.5)*rng.standard_normal()/math.sqrt(dt))*dt
            sim.r += g*D2R*dt
        sim.step(cmd, brk)
        out.append((sim.t, sim.r*R2D, sim.psi*R2D, sim.delta))
    return np.array(out)

JETS={"ifrit":(dict(m=25800,L=7.07,b=0.24,tr=4.2,mn=0.7,mm=0.8,d=(-1,1),kz=5.0),1.0,0.0),
      "f22e":(dict(m=25000,L=6.0,b=0.18,tr=2.74,mn=1.0,mm=1.0,d=(-1,-1),kz=4.6),2.0,10.0)}
def mk(p,mf=1,kf=1,db=0): return Jet("x",p["m"]*mf,p["L"],p["b"]+db,p["tr"],p["mn"],p["mm"],p["d"],kz=p["kz"]*kf)

if __name__=="__main__":
    dt=1/60
    for n,(p,T,minS) in JETS.items():
        print(f"== {n}: release after a 3 s full / half pedal turn. stop = |r| < 1 deg/s after release; heading overshoot after stop; heading drift 4 s later")
        for V in (4,7,10,15,20,25):
            for pa in (1.0,0.5):
                res={}
                for ind in (False,True):
                    a=run(mk(p),V,lambda t,pa=pa: pa if t<3 else 0.0,T,minS,ind,Tend=9)
                    i0=int(3/dt); r=a[i0:,1]; psi=a[i0:,2]
                    ist=next((k for k,x in enumerate(r) if abs(x)<1.0),None)
                    stop= ist*dt if ist is not None else float('nan')
                    # heading change from release to rest; swing-back = max reverse rate after stop
                    back = (np.max(-np.sign(r[0])*r[ist:]) if ist is not None else float('nan'))
                    res[ind]=(stop, psi[-1]-psi[0], back, np.max(np.abs(a[i0:,3])))
                s,i=res[False],res[True]
                print(f"  V {V:2d} m/s ({V*1.944:3.0f} kt) ped {pa:.1f} | stop s: 2.4.13 {s[0]:.2f} / INDI {i[0]:.2f} | heading after release deg: {s[1]:6.1f} / {i[1]:6.1f} | swing-back deg/s: {s[2]:.2f} / {i[2]:.2f}")
