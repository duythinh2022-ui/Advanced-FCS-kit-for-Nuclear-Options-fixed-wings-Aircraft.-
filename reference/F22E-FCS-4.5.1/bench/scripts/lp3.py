# convex-hull LP: max yaw (direction sgn) s.t. roll = Xq, pitch = Yq, each surface a convex combo of its moment samples
import re,sys,numpy as np
from scipy.optimize import linprog
def load(f):
    E={}
    for l in open(f):
        if l.startswith('#'): continue
        name=l.split()[0]
        s=re.findall(r'\[(-?\d+): (-?\d+),(-?\d+),(-?\d+)\]',l)
        E[name]=np.array([[float(a),float(x),float(y),float(z)] for a,x,y,z in s])
    return E
E=load(sys.argv[1]); sgn=float(sys.argv[2])
def solve(groups, Xq, Yq):
    cols=[];own=[]
    for gi,g in enumerate(groups):
        for row in g: cols.append(row); own.append(gi)
    cols=np.array(cols); n=len(cols); m=len(groups)
    c=-sgn*cols[:,3]
    Aeq=np.zeros((2+m,n)); beq=np.zeros(2+m)
    Aeq[0]=cols[:,1]; beq[0]=Xq; Aeq[1]=cols[:,2]; beq[1]=Yq
    for j,gi in enumerate(own): Aeq[2+gi,j]=1
    beq[2:]=1
    r=linprog(c,A_eq=Aeq,b_eq=beq,bounds=(0,None),method="highs")
    if not r.success: return float('nan'),None
    w=r.x; picks=[]
    for gi,g in enumerate(groups):
        idx=[j for j in range(n) if own[j]==gi]; picks.append(round(float(np.dot(w[idx],cols[idx,0])),1))
    return sgn*float(np.dot(w,cols[:,3])), picks
def G(n): return E[n][:,[0,1,2,3]]
def gangpair(nL,nR):
    out=[]
    for row in E[nL]:
        d=row[0]
        if not (E[nR][0,0]<=-d<=E[nR][-1,0]): continue
        b=[np.interp(-d,E[nR][:,0],E[nR][:,k]) for k in (1,2,3)]
        out.append([d,row[1]+b[0],row[2]+b[1],row[3]+b[2]])
    return np.array(out)
def gang4():
    out=[]
    for d in np.arange(-25,25.01,1.0):
        v=np.zeros(3)
        for n,s in (("Aileron_L",1),("Flap_L",1),("Aileron_R",-1),("Flap_R",-1)):
            v+=[np.interp(s*d,E[n][:,0],E[n][:,k]) for k in (1,2,3)]
        out.append([d,*v])
    return np.array(out)
tvc=G("TVC"); rud=[G("Rudder_L"),G("Rudder_R")]
sets={
 "today's pairing (flaperons ganged, stabs antisym)":[gang4(),gangpair("Elevator_L","Elevator_R"),tvc]+rud,
 "+ split flaperons (inner/outer, L/R free)":[G("Aileron_L"),G("Flap_L"),G("Aileron_R"),G("Flap_R"),gangpair("Elevator_L","Elevator_R"),tvc]+rud,
 "+ free stabs (TVC carries pitch)":[gang4(),G("Elevator_L"),G("Elevator_R"),tvc]+rud,
 "everything independent":[G(n) for n in E],
 "rudders only (roll from ganged surfaces)":[gang4(),gangpair("Elevator_L","Elevator_R"),tvc]+rud,
}
print("# yaw dir",sgn)
YQ=float(sys.argv[3]) if len(sys.argv)>3 else 0.0
for Xq in (0,300,450):
    print(f"roll {Xq}, pitch {YQ}:")
    for k,g in sets.items():
        z,p=solve(g,Xq,YQ); print(f"   {k:52s} yaw {z:6.1f}  picks {p}")
