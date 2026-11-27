# Vortex 0.5.13: hover / low-speed yaw about the pivot. usage: piv.py name:t_on:t_off ...   (csv in the current dir)
# CG and pivot (k ahead of the CG along the nose, k = -kSide of the run) side speeds and drifts, bank, yaw timing, height.
import csv,sys,math
g=lambda r,k: float(r[k])
for a in sys.argv[1:]:
    n,t0,t1=a.split(':'); t0=float(t0); t1=float(t1)
    R=list(csv.DictReader(open(n+'.csv')))
    seg=lambda ta,tb: [r for r in R if ta<=g(r,'t')<=tb]
    k=(-sum(g(r,'kSide') for r in seg(t0-2,t0))/max(1,len(seg(t0-2,t0)))) if 'kSide' in R[0] else 3.17
    dt=g(R[1],'t')-g(R[0],'t')
    def piv(r):
        h=math.radians(g(r,'psi')); return g(r,'pos_x')+k*math.sin(h), g(r,'pos_z')+k*math.cos(h)
    def vp(r): return g(r,'vSide')+k*math.radians(g(r,'psiRate'))
    A=seg(t0,t1+5); S=seg(t0,t1); E=seg(t1,t1+5)
    base=min(R,key=lambda r:abs(g(r,'t')-t0)); a0=g(base,'alt'); p0=piv(base); c0=(g(base,'pos_x'),g(base,'pos_z'))
    rmax=max(abs(g(r,'r')) for r in S)
    t90=next((g(r,'t')-t0 for r in S if abs(g(r,'r'))>=0.9*rmax),float('nan'))
    tstop=next((g(r,'t')-t1 for r in E if abs(g(r,'r'))<0.5),float('nan'))
    hd=lambda L:(lambda p:[(p[i]-p[0]+180)%360-180 for i in range(len(p))])([g(r,'psi') for r in L])
    def unwrap(L):
        o=[0.0]; 
        for i in range(1,len(L)):
            d=(g(L[i],'psi')-g(L[i-1],'psi')+180)%360-180; o.append(o[-1]+d)
        return o
    H=unwrap(A); iE=len(S)
    coast=abs(H[-1]-H[min(iE,len(H)-1)])
    pd=max(math.hypot(piv(r)[0]-p0[0],piv(r)[1]-p0[1]) for r in A); pe=math.hypot(piv(A[-1])[0]-p0[0],piv(A[-1])[1]-p0[1])
    cd=max(math.hypot(g(r,'pos_x')-c0[0],g(r,'pos_z')-c0[1]) for r in A)
    print(f"{n:13s} yaw {rmax:4.1f} deg/s 90% +{t90:.2f}s stop {tstop:.2f}s turned {abs(H[-1]):5.1f} (after release {coast:4.1f}) | pivot skid {max(abs(vp(r)) for r in A):.2f} m/s drift {pd:.2f} end {pe:.2f} m | CG skid {max(abs(g(r,'vSide')) for r in A):.2f} drift {cd:.2f} m | bank {max(abs(g(r,'phi')) for r in A):.1f} pitch {min(g(r,'theta') for r in A):+.1f}/{max(g(r,'theta') for r in A):+.1f} | height {min(g(r,'alt') for r in A)-a0:+.2f}/{max(g(r,'alt') for r in A)-a0:+.2f}")
