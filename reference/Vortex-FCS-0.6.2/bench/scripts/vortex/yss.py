# Vortex 0.5.11: side skid from the hover yaw. usage: yss.py name:t_on:t_off ...
import csv,sys,math
g=lambda r,k: float(r[k])
for a in sys.argv[1:]:
    n,t0,t1=a.split(':'); t0=float(t0); t1=float(t1)
    R=list(csv.DictReader(open(n+'.csv')))
    def seg(ta,tb): return [r for r in R if ta<=g(r,'t')<=tb]
    S=seg(t0,t1); E=seg(t1,t1+5)
    r0=R[0]; base=min(R,key=lambda r:abs(g(r,'t')-t0))
    x0,z0=g(base,'pos_x'),g(base,'pos_z'); a0=g(base,'alt')
    rmax=max(abs(g(r,'r')) for r in S)
    t90=next((g(r,'t')-t0 for r in S if abs(g(r,'r'))>=0.9*rmax),None)
    tstop=next((g(r,'t')-t1 for r in E if abs(g(r,'r'))<0.5),None)
    vs=lambda L: max(abs(g(r,'vSide')) for r in L)
    drift=max(math.hypot(g(r,'pos_x')-x0,g(r,'pos_z')-z0) for r in seg(t0,t1+5))
    endd=math.hypot(g(E[-1],'pos_x')-x0,g(E[-1],'pos_z')-z0)
    A=seg(t0,t1+5)
    print(f"{n:11s} yaw {rmax:4.1f} deg/s (90% at +{t90:.2f}s, stop to 0.5 in {tstop:.2f}s) | skid: start {vs(seg(t0,t0+2.5)):.2f}, stop {vs(E):.2f} m/s | drift max {drift:.2f} m, end {endd:.2f} m | bank max {max(abs(g(r,'phi')) for r in A):.1f} | height {min(g(r,'alt') for r in A)-a0:+.2f}/{max(g(r,'alt') for r in A)-a0:+.2f}")
