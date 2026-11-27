# Vortex 0.5.14: a command just after (or with) a climb. usage: ax2.py <tag> [<tag> ...]   (csv in /tmp/ax or $AXDIR)
import csv,sys,os
D=os.environ.get('AXDIR','/tmp/ax'); f=lambda r,k: float(r[k])
def load(n): return list(csv.DictReader(open(D+'/'+n+'.csv')))
def first(S,t0,cond): return next((f(r,'t')-t0 for r in S if cond(r)),float('nan'))
for tag in sys.argv[1:]:
    print('==',tag)
    for n in ['C0','CS5','CS0','SC5','CSh','CF5','CY5']:
        R=load(n+'_'+tag); tc={'C0':20.5,'CS5':20.5,'CS0':21,'SC5':21.5,'CSh':20.5,'CF5':20.5,'CY5':20.5}[n]
        S=[r for r in R if f(r,'t')>=tc]; a0=f(S[0],'alt'); W=[r for r in R if tc<=f(r,'t')<=27]
        print(f"  {n:4s} climb: to 1 m/s {first(S,tc,lambda r:f(r,'vs')>=1):.2f}s, to 2.5 m/s {first(S,tc,lambda r:f(r,'vs')>=2.5):.2f}s, height gained in 3 s {f(min(R,key=lambda r:abs(f(r,'t')-tc-3)),'alt')-a0:.2f} m, lowest vs {min(f(r,'vs') for r in W):+.2f}, thrust max {max(f(r,'thrEng') for r in W):.2f}")
    for n in ['S0','CS5','CS0','SC5','Sh','CSh']:
        R=load(n+'_'+tag); S=[r for r in R if f(r,'t')>=21]; W=[r for r in R if 21<=f(r,'t')<=31]
        print(f"  {n:4s} side: bank at +1 s {abs(f(min(R,key=lambda r:abs(f(r,'t')-22)),'phi')):.1f}, to 1 m/s {first(S,21,lambda r:abs(f(r,'vSide'))>=1):.2f}s, to 3 m/s {first(S,21,lambda r:abs(f(r,'vSide'))>=3):.2f}s, top {max(abs(f(r,'vSide')) for r in W):.2f} m/s, bank max {max(abs(f(r,'phi')) for r in W):.1f}, govLam min {min(f(r,'govLam') for r in W):.2f}")
    for n in ['F0','CF5']:
        R=load(n+'_'+tag); S=[r for r in R if f(r,'t')>=21]; W=[r for r in R if 21<=f(r,'t')<=31]
        print(f"  {n:4s} lever: to 2 m/s {first(S,21,lambda r:f(r,'uFwd')>=2):.2f}s, to 8 m/s {first(S,21,lambda r:f(r,'uFwd')>=8):.2f}s, pitch {min(f(r,'theta') for r in W):+.1f}/{max(f(r,'theta') for r in W):+.1f}")
    for n in ['K30','K30C']:
        R=load(n+'_'+tag); S=[r for r in R if f(r,'t')>=30]; A=[r for r in R if 30<=f(r,'t')<=43]; k=3.17
        print(f"  {n:4s} 30 kt pedal: half rate {first(S,30,lambda r:abs(f(r,'psiRate'))>=6.7):.2f}s, 90% {first(S,30,lambda r:abs(f(r,'psiRate'))>=12.06):.2f}s, pivot skid {max(abs(f(r,'vSide')+k*f(r,'psiRate')*0.01745) for r in A):.2f} m/s, bank max {max(abs(f(r,'phi')) for r in A):.1f}, lowest vs {min(f(r,'vs') for r in A):+.2f}")
    R=load('CY5_'+tag); S=[r for r in R if f(r,'t')>=21]
    print(f"  CY5  hover pedal after climb: half rate {first(S,21,lambda r:abs(f(r,'psiRate'))>=12):.2f}s")
