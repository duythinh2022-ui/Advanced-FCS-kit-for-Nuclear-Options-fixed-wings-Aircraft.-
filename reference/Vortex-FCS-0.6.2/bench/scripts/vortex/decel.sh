#!/bin/bash
# airborne conversion at 380 km/h with the lever in the aft detent, decel to the hover. usage: decel.sh <tag> [args]
tag=$1; shift
timeout 900 /home/claude/vortex/b.sh stovl ias=380 alt=300 T=75 tog=1 "thr=0:0.3,0.5:0.20" print=5 csv=/tmp/lg/decel_$tag.csv mass=11040 ThrustScale=1.12 DragScale=0.885 "$@" > /tmp/lg/decel_$tag.txt 2>&1
python3 - $tag <<'PY'
import sys,pandas as pd,numpy as np
d=pd.read_csv(f'/tmp/lg/decel_{sys.argv[1]}.csv')
on=d[d.state==2]; t0=on.t.iloc[0]; h0=on.alt.iloc[0]
def tat(v):
    w=on[on.ias<=v]; return (w.t.iloc[0]-t0) if len(w) else float('nan')
print(f"{sys.argv[1]:8s} STOVL on at t {t0:.1f}, {on.ias.iloc[0]:.0f} km/h | to 200 km/h {tat(200):.1f}s, 100 km/h {tat(100):.1f}s, gs<1kt {(on[on.gs<1].t.iloc[0]-t0) if (on.gs<1).any() else float('nan'):.1f}s | alt dev {on.alt.min()-h0:+.1f}/{on.alt.max()-h0:+.1f} m, vs {on.vs.min():+.2f}/{on.vs.max():+.2f} | airbrake open: mean {on[on.ias>150].abOpen.mean():.2f} above 150 km/h, at 300/200/120 km/h: "+'/'.join(f"{on.iloc[(on.ias-v).abs().values.argmin()].abOpen:.2f}" for v in (300,200,120))+f" | decel mean above 150: {-(on[on.ias>150].ias.diff()/3.6/on[on.ias>150].t.diff()).mean():.2f} m/s2, pitch {on.theta.min():.0f}..{on.theta.max():.0f}, thr max {on.thrEng.max():.2f}")
PY
