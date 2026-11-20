import csv,sys,math
g=lambda r,k: float(r[k])
for n,t0,d,T in [(a,float(b),float(c),float(e)) for a,b,c,e in (x.split(':') for x in sys.argv[1:])]:
    R=list(csv.DictReader(open(f'/tmp/py/{n}.csv'))); w=[r for r in R if t0<=g(r,'t')<=t0+d]; a=[r for r in R if t0+d<g(r,'t')<=T]
    at=lambda t: min(R,key=lambda r:abs(g(r,'t')-t)); r0=at(t0-0.05); psi0=g(r0,'psi')
    rr=[abs(g(r,'r')) for r in w]; ss=[r for r in w if g(r,'t')>t0+2]
    print(f"{n}: yaw rate steady {sum(abs(g(r,'r')) for r in ss)/len(ss):.1f} (peak {max(rr):.1f}) deg/s, heading {((g(w[-1],'psi')-psi0+540)%360-180):+.0f}; skid |vSide| max {max(abs(g(r,'vSide')) for r in w):.2f} m/s; |beta| max {max(abs(g(r,'beta')) for r in w):.1f}; bank max {max(abs(g(r,'phi')) for r in w):.1f}; fwd speed min {min(g(r,'uFwd') for r in w):.1f} m/s | after release: bank other way {max([-g(r,'phi')*math.copysign(1,g(w[-1],'phi')) for r in a]+[0]):.1f}, skid max {max(abs(g(r,'vSide')) for r in a):.2f}, heading overshoot {((g(a[-1],'psi')-g(w[-1],'psi')+540)%360-180):+.1f}; alt {min(g(r,'alt') for r in w+a)-g(r0,'alt'):+.2f}/{max(g(r,'alt') for r in w+a)-g(r0,'alt'):+.2f}")
