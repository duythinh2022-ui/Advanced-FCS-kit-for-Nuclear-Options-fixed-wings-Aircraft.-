import json,sys,math
import numpy as np
from scipy.optimize import linprog
def load(f): return [json.loads(l) for l in open(f)]
SETS={"surf":["Elevator_L","Elevator_R","Aileron_L","Aileron_R","Flap_L","Flap_R"],
      "surf+split":["Elevator_L","Elevator_R","Aileron_L","Aileron_R","Flap_L","Flap_R","Split_L","Split_R"],
      "surf+tvc":["Elevator_L","Elevator_R","Aileron_L","Aileron_R","Flap_L","Flap_R","TVC_L","TVC_R"],
      "all":["Elevator_L","Elevator_R","Aileron_L","Aileron_R","Flap_L","Flap_R","Split_L","Split_R","TVC_L","TVC_R"]}
def maxroll(rec,names):
    a=math.radians(rec["alpha"]); ca,sa=math.cos(a),math.sin(a)
    cols=[]
    for n in names:
        for d in rec["eff"][n]: cols.append(d)
    D=np.array(cols).T  # 3 x m  (p,q,r)
    pS=D[0]*ca+D[2]*sa; rS=D[2]*ca-D[0]*sa; q=D[1]
    res=linprog(-pS,A_eq=np.vstack([rS,q]),b_eq=[0,0],bounds=[(0,1)]*D.shape[1],method="highs")
    return -res.fun if res.status==0 else float('nan')
def maxyawS(rec,names):  # max stability-axis yaw accel with pS=0,q=0 (sideslip control power)
    a=math.radians(rec["alpha"]); ca,sa=math.cos(a),math.sin(a)
    cols=[d for n in names for d in rec["eff"][n]]
    D=np.array(cols).T; pS=D[0]*ca+D[2]*sa; rS=D[2]*ca-D[0]*sa
    res=linprog(-rS,A_eq=np.vstack([pS,D[1]]),b_eq=[0,0],bounds=[(0,1)]*D.shape[1],method="highs")
    return -res.fun if res.status==0 else float('nan')
f=sys.argv[1]; R=load(f)
ias=sorted(set(r["ias"] for r in R))
for metric,fn in (("max coordinated velocity-vector roll accel pS (rS=0, dq=0), deg/s^2",maxroll),("max stability-axis yaw accel rS (pS=0, dq=0), deg/s^2",maxyawS)):
    print("\n##",f.split('/')[-1],metric)
    for sname,names in SETS.items():
        print(f"\n[{sname}]  alpha: "+" ".join(f"{a:>5d}" for a in range(0,91,5)))
        for v in ias:
            row=[fn(r,names) for r in R if r["ias"]==v]
            print(f"  IAS {v:>4.0f}        "+" ".join(f"{x:5.0f}" for x in row))
