# Load sensitivity of stock-law ground steering on gsim's plant: mass x (0.8, 1, 1.4), yaw radius of gyration
# up (wing stores), CG shift (nose load). Metrics: steady r at full/half pedal, 63% rise, stop after release.
import math, sys
sys.path.insert(0,".")
from gsim import Jet, Sim
R2D=180/math.pi
def stock(ped,r,V,T,minS): return ped if V<minS else max(-1,min(1,T*(ped-r)))
def run(j,V,pa,T,minS,dt=0.02):
    s=Sim(j,V,dt); rs=[]
    while s.t<6-1e-9:
        p=pa if s.t<3 else 0.0
        c=stock(p,s.r,V,T,minS); s.step(c,c); rs.append(s.r*R2D)
    rss=rs[int(2.9/dt)]
    i63=next(k for k,x in enumerate(rs) if abs(x)>=0.63*abs(rss))*dt
    after=rs[150:]; ist=next((k for k,x in enumerate(after) if abs(x)<0.5),len(after))*dt
    return rss,i63,ist
base={"ifrit":dict(m=25800,L=7.07,b=0.24,tr=4.2,mn=0.7,mm=0.8,d=(-1,1),kz=5.0,T=1.0,ms=0.0),
      "f22e":dict(m=25000,L=6.0,b=0.18,tr=2.74,mn=1.0,mm=1.0,d=(-1,-1),kz=4.6,T=2.0,ms=10.0)}
cases=[("base",1.0,1.0,0.0),("light 0.8m",0.8,1.0,0.0),("heavy 1.4m",1.4,1.0,0.0),
       ("heavy+wing stores kz+15%",1.4,1.15,0.0),("heavy, CG fwd (+4% nose)",1.4,1.0,0.04),("heavy, CG aft (-4% nose)",1.4,1.0,-0.04)]
for n,p in base.items():
    print("==",n)
    for V in (8,15,20):
        for lab,mf,kf,db in cases:
            j=Jet(n,p["m"]*mf,p["L"],p["b"]+db,p["tr"],p["mn"],p["mm"],p["d"],kz=p["kz"]*kf)
            out=[run(j,V,pa,p["T"],p["ms"]) for pa in (1.0,0.5)]
            print(f"  V {V:2d} m/s ({V*1.944:3.0f} kt) {lab:26s} | r full {out[0][0]:5.1f} half {out[1][0]:5.1f} deg/s | 63% {out[0][1]:.2f}/{out[1][1]:.2f} s | stop<0.5 {out[0][2]:.2f}/{out[1][2]:.2f} s")
