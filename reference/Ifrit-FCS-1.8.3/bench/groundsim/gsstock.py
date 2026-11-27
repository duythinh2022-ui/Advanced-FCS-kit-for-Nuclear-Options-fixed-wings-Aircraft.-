# true stock (ControlsFilter -> FlyByWire yaw law) vs 2.4.11 (raw pedal) on gsim's plant
import math, sys
import numpy as np
sys.path.insert(0, ".")
from gsim import Jet, Sim
R2D=180/math.pi
def stock_yaw(ped, r_rad, V, T, minSpeed, wv=0.0, beta=0.0):
    if V < minSpeed: return ped        # ControlsFilter returns before the FBW (on the ground, radarAlt < minAlt)
    return max(-1.0, min(1.0, T*(ped - r_rad) + wv*beta))
def run(jet, V, pf, mode, T, minS, Tend=6.0, dt=0.02):
    s=Sim(jet,V,dt); out=[]
    while s.t < Tend-1e-9:
        p=pf(s.t)
        cmd = stock_yaw(p, s.r, V, T, minS) if mode=="stock" else p
        s.step(cmd, cmd if mode=="stock" else p)   # differential brake reads the (filtered) yaw input in stock
        out.append((s.r*R2D, s.vy/V*R2D if V>0 else 0))
    return np.array(out)
jets={"ifrit":(Jet("ifrit",25800,7.07,0.24,4.2,0.7,0.8,(-1,1),kz=5.0),1.0,0.0),
      "f22e":(Jet("f22e",25000,6.0,0.18,2.74,1.0,1.0,(-1,-1),kz=4.6),2.0,10.0)}
for n,(j,T,minS) in jets.items():
    print(f"== {n} (stock yawTightness {T}, FBW above {minS} m/s)")
    print("  V m/s (kt) | pedal | steady r deg/s: stock / raw(2.4.11) | radius m: stock / raw | sideslip deg: stock / raw | stop after release s: stock / raw")
    for V in (5,8,12,16,20,25,35,50):
        for pa in (1.0,0.5,0.2):
            res={}
            for m in ("stock","raw"):
                a=run(j,V,lambda t,pa=pa: pa if t<3 else 0.0,m,T,minS)
                rss=a[int(2.9/0.02),0]; b=a[int(2.9/0.02),1]
                after=a[150:,0]; ist=next((k for k,x in enumerate(after) if abs(x)<1.0),None)
                res[m]=(rss, V/max(abs(rss)/R2D,1e-6), b, ist*0.02 if ist is not None else float('nan'))
            s,w=res["stock"],res["raw"]
            print(f"  {V:3d} ({V*1.944:4.0f}) | {pa:.1f} | {s[0]:5.1f} / {w[0]:5.1f} | {s[1]:6.1f} / {w[1]:6.1f} | {s[2]:5.1f} / {w[2]:5.1f} | {s[3]:.2f} / {w[3]:.2f}")
