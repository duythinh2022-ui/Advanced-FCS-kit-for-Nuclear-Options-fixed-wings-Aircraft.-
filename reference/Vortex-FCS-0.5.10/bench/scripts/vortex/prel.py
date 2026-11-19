# Vortex 0.5.8: the pirouette after the pedal is let go / while held. usage: prel.py name:t_release[:t_end] ...
import csv,sys
def g(r,k): return float(r[k])
for a in sys.argv[1:]:
    p=a.split(':'); n=p[0]; t1=float(p[1]); te=float(p[2]) if len(p)>2 else t1+15
    R=[r for r in csv.DictReader(open(n+'.csv'))]
    at=lambda t: min(R,key=lambda r:abs(g(r,'t')-t))
    r1=at(t1); v1=g(r1,'vSide'); psi1=g(r1,'psi'); a1=g(r1,'alt')
    W=[r for r in R if t1<=g(r,'t')<=te]
    def first(cond):
        for r in W:
            if cond(r): return g(r,'t')-t1
        return None
    pk=max(W[:int(len(W))],key=lambda r:abs(g(r,'vSide')))
    tdec=first(lambda r: abs(g(r,'vSide'))<abs(v1)-0.3)
    tb=first(lambda r: abs(g(r,'phi'))>=10)
    t1ms=first(lambda r: abs(g(r,'vSide'))<1.0)
    ys=first(lambda r: abs(g(r,'r'))<0.5)
    dpsi=(g(at(t1+ys if ys else t1),'psi')-psi1+540)%360-180
    fmt=lambda x: '-' if x is None else '%.2f'%x
    print(f"{n:10s} release {t1}: side speed {v1:+.1f} -> peak {g(pk,'vSide'):+.1f} at +{g(pk,'t')-t1:.2f}s; falling from +{fmt(tdec)}s; bank 10 deg at +{fmt(tb)}s; under 1 at +{fmt(t1ms)}s; yaw stopped +{fmt(ys)}s ({dpsi:+.1f} deg more heading); |bank| max {max(abs(g(r,'phi')) for r in W):.1f}; alt {min(g(r,'alt') for r in W)-a1:+.2f}/{max(g(r,'alt') for r in W)-a1:+.2f}")
