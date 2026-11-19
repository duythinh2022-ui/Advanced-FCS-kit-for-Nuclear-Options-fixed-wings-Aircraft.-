#!/bin/bash
# VTO from idle, hover, pedal, crab, both
timeout 900 /home/claude/vortex/b.sh stovl ground=1 ias=0 T=62 tog=0.5 "thr=0:0" "pitch=0:0,8:0,8.3:0.6,14:0.6,14.3:0" "yaw=0:0,20:0,20.2:1,23:1,23.2:0,45:0,45.2:-1,49:-1,49.2:0" "roll=0:0,32:0,32.2:1,37:1,37.2:0,45:0,45.2:1,50:1,50.2:0" print=100 csv=$1 "${@:2}" >/dev/null 2>&1
python3 - "$1" <<'PY'
import sys,pandas as pd
d=pd.read_csv(sys.argv[1])
def seg(a,b,lab):
    w=d[(d.t>=a)&(d.t<b)]
    print(f"{lab:16s} t{a:4.0f}-{b:4.0f}: r {w.r.min():6.1f}..{w.r.max():6.1f}  vs {w.vs.min():5.2f}..{w.vs.max():5.2f}  alt {w.alt.min():5.1f}..{w.alt.max():5.1f}  gs {w.gs.min():4.1f}..{w.gs.max():4.1f}  phi {w.phi.min():5.1f}..{w.phi.max():5.1f}  thrEng max {w.thrEng.max():.2f}  authR {w.authR.mean():.0f} authP {w.authP.mean():.0f}")
seg(8,20,'liftoff+hover'); seg(20,32,'pedal 3s'); seg(32,45,'crab 5s'); seg(45,62,'pedal+crab')
w=d[(d.t>=20)&(d.t<32)]
r=w.r.values; t=w.t.values
import numpy as np
i=np.argmax(np.abs(r)); 
t30=t[np.argmax(np.abs(r)>0.9*abs(r[i]))]
after=w[w.t>23.2]; tstop=after[after.r.abs()<2].t.min()
print(f"pedal: peak r {r[i]:.1f} at t {t[i]:.2f}, 90% at {t30:.2f} (input 20.2), rate < 2 deg/s at {tstop:.2f} (released 23.2); heading change {w.psi.iloc[-1]-w.psi.iloc[0]:.0f}")
print("psi samples:", ' '.join(f"{x:.0f}" for x in d[(d.t>=20)&(d.t<30)].psi.values[::30]))
PY
