import pandas as pd, numpy as np, sys, warnings; warnings.filterwarnings('ignore')
R='/home/claude/su30/v10/src/bench/runs/'
def hf(x): return (x - x.rolling(60,center=True,min_periods=1).mean()).std()
for tag in sys.argv[1].split(','):
  for ias in [250,350]:
    try: d=pd.read_csv(f'{R}{tag}_lf_{ias}.csv')
    except: continue
    a=np.radians(d.alpha); d['pS']=d.p*np.cos(a)+d.r*np.sin(a)
    w=d[(d.t>4)&(d.t<14)]; nozS=(w.nozL+w.nozR)/2
    print(f'{tag} {ias}: AoA mean {w.alpha.mean():.1f} min {w.alpha.min():.1f} | hf q {hf(w.q):.2f} p {hf(w.p):.2f} r {hf(w.r):.2f} pS {hf(w.pS):.2f} nozS {hf(nozS):.2f} elev {hf(w.elevator_L_now):.2f} rud {hf(w.rudder_L_now):.2f} flap {hf(w.flaperon_L_now):.2f} can {hf(w.canard_L_now):.2f} | nozS mean {nozS.mean():.1f} vv {w.pS.abs().mean():.1f} ias {w.ias.mean()*3.6:.0f}')
