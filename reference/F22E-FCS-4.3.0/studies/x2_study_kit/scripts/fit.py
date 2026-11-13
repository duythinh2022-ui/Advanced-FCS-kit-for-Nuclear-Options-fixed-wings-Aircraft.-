import os
import json,sys,numpy as np
from scipy.optimize import lsq_linear
d=json.load(open(sys.argv[1])); AL=np.array(d['AL'],float); R=d['res']
I=307237.0
N0=np.array(R['base'][0]); q0=np.array(R['base'][1])
keys=['cg+0.1','lerxKv1','aftWin0.5','fore1.2','aft1.2']
dQ=np.array([np.array(R[k][1])-q0 for k in keys]).T   # q change per basis unit
m_lo=float(sys.argv[2]) if len(sys.argv)>2 else 0.05
fixKv=float(sys.argv[3]) if len(sys.argv)>3 else None
def mstar(a): return np.where(a<=10,m_lo,np.where(a<=36,m_lo*(36-a)/26,0.0))
# target increments between grid points (0..44)
rows=[];rhs=[];w=[]
idx=[i for i,a in enumerate(AL) if a<=44]
for i in idx[:-1]:
    am=(AL[i]+AL[i+1])/2; dN=N0[i+1]-N0[i]
    tgt=-mstar(am)*dN/I*57.2958
    rows.append(dQ[i+1]-dQ[i]); rhs.append(tgt-(q0[i+1]-q0[i])); w.append(1.0)
i36=list(AL).index(36)
rows.append(dQ[i36]*0.3); rhs.append((0-q0[i36])*0.3)   # q(36) ~ 0, light weight
A=np.array(rows); b=np.array(rhs)
lb=[-0.5,0,0,-2,-2]; ub=[2.5,4,4,2,4]
if fixKv is not None: lb[1]=ub[1]-1e-9 if False else fixKv-1e-6; ub[1]=fixKv+1e-6
r=lsq_linear(A,b,bounds=(lb,ub))
x=r.x
print('fit: cg shift %.3f m (total CGShiftAft %.3f), LerxVortexKv %.2f, aft window bump %.2f, BodyLiftFore %.3f, BodyLiftAft %.3f'%(0.1*x[0],0.05+0.1*x[0],x[1],0.5*x[2],1+0.2*x[3],1+0.2*x[4]))
q=q0+dQ@x
print(' AoA  q_base  q_fit  margin_base  margin_fit  target')
for i in range(1,len(AL)-1):
    a=AL[i]; dN=N0[i+1]-N0[i-1]
    mb=-(q0[i+1]-q0[i-1])/57.2958*I/dN; mf=-(q[i+1]-q[i-1])/57.2958*I/dN
    if a%4==0: print('%4d %7.1f %7.1f %8.3f %8.3f %8.3f'%(a,q0[i],q[i],mb,mf,mstar(a)))
