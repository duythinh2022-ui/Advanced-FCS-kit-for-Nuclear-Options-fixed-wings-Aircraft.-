import csv,sys,math
def load(f): return list(csv.DictReader(open(f)))
def ana(name,t0,dur,T):
    R=load(f'/tmp/py/{name}.csv'); g=lambda r,k: float(r[k])
    at=lambda t: min(R,key=lambda r:abs(g(r,'t')-t))
    r0=at(t0-0.05); psi0=g(r0,'psi'); a0=g(r0,'alt'); p0=(g(r0,'pos_x'),g(r0,'pos_z'))
    w=[r for r in R if t0<=g(r,'t')<=t0+dur]; w2=[r for r in R if t0+dur<=g(r,'t')<=T-0.1]
    def dpsi(r): return (g(r,'psi')-psi0+540)%360-180
    rates=[abs(g(r,'r')) for r in w]
    # track direction change
    def trk(r1,r2): return math.degrees(math.atan2(g(r2,'pos_x')-g(r1,'pos_x'),g(r2,'pos_z')-g(r1,'pos_z')))
    re=at(t0+dur)
    tr0=trk(at(t0-1.0),r0); trE=trk(at(t0+dur-1.0),re)
    print(f"{name}: before gs {g(r0,'gs'):.1f} m/s ({g(r0,'gs')*1.944:.0f} kt) ias {g(r0,'ias'):.1f}")
    for dt in (1,2,3,5,8):
        r=at(t0+dt); print(f"   +{dt}s: heading {dpsi(r):+6.1f} deg  r {g(r,'r'):+5.1f} deg/s  beta {g(r,'beta'):+6.1f}  phi {g(r,'phi'):+5.1f}  gs {g(r,'gs'):5.1f}  alt {g(r,'alt')-a0:+.2f}")
    print(f"   pedal held: peak yaw rate {max(rates):.1f} deg/s, mean {sum(rates)/len(rates):.1f}; heading change {dpsi(re):+.0f} deg; ground track turned {((trE-tr0+540)%360-180):+.0f} deg; |beta| max {max(abs(g(r,'beta')) for r in w):.1f}; |phi| max {max(abs(g(r,'phi')) for r in w):.1f}; alt {min(g(r,'alt') for r in w)-a0:+.2f}/{max(g(r,'alt') for r in w)-a0:+.2f}; gs min {min(g(r,'gs') for r in w):.1f}")
    rr=at(T-0.2)
    print(f"   after release ({T-t0-dur:.0f} s): heading {dpsi(rr):+.0f}, overshoot past release heading {dpsi(rr)-dpsi(re):+.1f}; |phi| max {max(abs(g(r,'phi')) for r in w2):.1f}; |beta| at end {abs(g(rr,'beta')):.1f}; gs end {g(rr,'gs'):.1f}; alt band {min(g(r,'alt') for r in w2)-a0:+.2f}/{max(g(r,'alt') for r in w2)-a0:+.2f}")
for n,t0,d,T in [(a,float(b),float(c),float(e)) for a,b,c,e in (x.split(':') for x in sys.argv[1:])]: ana(n,t0,d,T)
