#!/bin/bash
# VTO from idle to ~35 m, hover, full push to touchdown; $1 csv, $2 extra args (mass=...)
timeout 900 /home/claude/vortex/b.sh stovl ground=1 ias=0 T=50 tog=0.5 "thr=0:0" "pitch=0:0,8:0,8.3:1,16:1,16.3:0,24:0,24.3:-1,48:-1" print=100 csv=$1 "${@:2}" > $1.log 2>&1
python3 - "$1" <<'PY'
import sys,pandas as pd
d=pd.read_csv(sys.argv[1])
air=d[(d.t>9)&(d.alt>2.3)]
print(f"mass case: aUp hover {d[(d.t>18)&(d.t<24)].aUp.mean():.2f} m/s^2, climbLim {d[(d.t>18)&(d.t<24)].climbLim.mean():.1f}, peak alt {d.alt.max():.1f}, max climb {d[(d.t>8)&(d.t<17)].vs.max():.2f}")
w=d[d.t>24]
td=w[w.alt<=w.alt.min()+0.05].t.min() if len(w) else -1
for t in [24.5,26,28,30,32,34,36,38,40,42,44]:
    x=d[d.t>=t]
    if len(x)==0: break
    x=x.iloc[0]; print(f"  t {t:4.1f} alt {x.alt:6.2f} vs {x.vs:6.2f} sinkLim {x.sinkLim:5.2f} thrEng {x.thrEng:.2f} gs {x.gs:.1f}")
# touchdown: first time gear load... use alt minimum reached and vs just before
w2=w[w.alt<2.15]
if len(w2): 
    i=w2.index[0]; print(f"touchdown ~t {d.loc[i,'t']:.2f}: vs {d.loc[i-3:i+1,'vs'].min():.2f} m/s")
PY
