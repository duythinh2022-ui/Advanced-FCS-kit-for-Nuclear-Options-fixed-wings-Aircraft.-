# Can any controller recover the heavy+stores response? Hold the same target rate as base stock, but drive the
# wheel with a very stiff loop (gain 20, i.e. bang-bang into lock): the best the gear/tyres allow.
import math, sys
sys.path.insert(0,"."); from gsim import Jet, Sim
R2D=180/math.pi
def run(j,V,rt,K,dt=0.02):
    s=Sim(j,V,dt); rs=[]
    while s.t<3-1e-9:
        c=max(-1,min(1,K*(rt-s.r*R2D)/R2D)); s.step(c,c); rs.append(s.r*R2D)
    return next(k for k,x in enumerate(rs) if x>=0.63*rt)*dt
for n,a in {"ifrit":(25800,7.07,0.24,4.2,0.7,0.8,(-1,1),5.0),"f22e":(25000,6.0,0.18,2.74,1.0,1.0,(-1,-1),4.6)}.items():
    m,L,b,tr,mn,mm,d,kz=a
    for V,rt in ((8,13.6),(15,17.8),(20,19.5)):
        base=Jet(n,m,L,b,tr,mn,mm,d,kz=kz); st=Jet(n,m*1.4,L,b,tr,mn,mm,d,kz=kz*1.15)
        print(f"{n} V {V} m/s target {rt} deg/s | 63% rise, stiff loop: base {run(base,V,rt,20):.2f} s, heavy+stores {run(st,V,rt,20):.2f} s")
