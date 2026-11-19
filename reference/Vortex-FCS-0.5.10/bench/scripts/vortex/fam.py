import csv,sys
def load(f): return list(csv.DictReader(open(f)))
for case,t0 in (('H',20.0),('F',24.0)):
    for v in sys.argv[1:]:
        R=load(f'/tmp/fa/{case}{v}.csv'); g=lambda row,k: float(row[k])
        pre=[r for r in R if t0-1<=g(r,'t')<t0]; post=[r for r in R if t0<=g(r,'t')<=t0+16]
        a0=g(pre[-1],'alt')
        at=lambda dt: min(post,key=lambda r:abs(g(r,'t')-(t0+dt)))
        print(f"{case}{v}: before: alt {a0:.2f} vs {g(pre[-1],'vs'):.2f} gs {g(pre[-1],'gs'):.1f} thrIn {g(pre[-1],'thrIn'):.3f} thrEng {g(pre[-1],'thrEng'):.3f} rpm {g(pre[-1],'rpm'):.0f}")
        for dt in (0.1,0.5,1,2,3,5,8,12,15):
            r=at(dt); print(f"   +{dt:>4}s: thrEng {g(r,'thrEng'):.3f} rpm {g(r,'rpm'):.0f} alt {g(r,'alt')-a0:+.2f} m vs {g(r,'vs'):+.2f} gs {g(r,'gs'):.1f} theta {g(r,'theta'):.1f} swivel {g(r,'swivel'):.3f} law {r['law']}")
        w=[r for r in post if g(r,'t')<=t0+5]; print(f"   first 5 s: alt min {min(g(r,'alt') for r in w)-a0:+.2f} max {max(g(r,'alt') for r in w)-a0:+.2f}, vs min {min(g(r,'vs') for r in w):+.2f} max {max(g(r,'vs') for r in w):+.2f}")
