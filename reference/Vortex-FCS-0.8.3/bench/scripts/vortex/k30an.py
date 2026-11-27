import pandas as pd,numpy as np,sys
def piv(n):
    d=pd.read_csv(f'/tmp/ax/{n}.csv'); d['vp']=d.vPiv; return d
for tag in sys.argv[1:]:
  for c,t0 in [('K30',30),('K30C',30),('K45',35),('K45C',35),('KH30',30),('KD30',30)]:
    try: d=piv(f'{c}_{tag}')
    except Exception as e: print(c,tag,'missing'); continue
    s=lambda a,b: d[(d.t>=t0+a)&(d.t<t0+b)]
    w=s(8,13); ip=w.vp.abs().idxmax()
    h0=d[(d.t>t0-1)&(d.t<t0-0.5)].alt.mean()
    print(f"{c+'_'+tag:10s} pivot side: turn-in {s(0,4).vp.abs().max():.2f} steady {s(4,8).vp.abs().max():.2f} after release {w.vp.abs().max():.2f} (t+{d.t[ip]-t0-8:.2f}) | yaw rate 1-4 s {s(1,4).psiRate.abs().mean():5.1f} 5-8 s {s(5,8).psiRate.abs().mean():5.1f} | bank max {s(0,13).phi.abs().max():4.1f} | vs min {s(0,15).vs.min():+.2f} max {s(0,15).vs.max():+.2f} | climb acc 0.5-2.5 s {(s(2.4,2.6).vs.mean()-s(0.4,0.6).vs.mean())/2:.2f} ")
