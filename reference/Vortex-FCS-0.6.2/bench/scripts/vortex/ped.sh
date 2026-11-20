#!/bin/bash
# hover pedal turns (09:42 log): full left pedal 12.5 s and release, then 3 s and release. usage: ped.sh <tag> [args]
tag=$1; shift
timeout 600 /home/claude/vortex/b.sh stovl ground=1 ias=0 T=60 tog=0.5 "thr=0:0,5:0,5.5:0.20" "pitch=0:0,8:0,8.3:0.6,13:0.6,13.3:0" "yaw=0:0,25:0,25.1:-1,37.5:-1,37.6:0,45:0,45.1:-1,48:-1,48.1:0" print=100 csv=/tmp/lg/ped_$tag.csv mass=${M:-12150} ThrustScale=1.12 DragScale=0.885 "$@" > /tmp/lg/ped_$tag.txt 2>&1
python3 - $tag <<'PY'
import sys,pandas as pd,numpy as np
t=sys.argv[1]; b=pd.read_csv(f'/tmp/lg/ped_{t}.csv')
x0=b.pos_x[b.t.between(24,25)].mean(); z0=b.pos_z[b.t.between(24,25)].mean(); h0=b.alt[b.t.between(24,25)].mean()
def seg(a,c):
    w=b[(b.t>=a)&(b.t<c)]; dist=np.hypot(w.pos_x-x0,w.pos_z-z0)
    return f"gs max {w.gs.max()/1.944:4.2f} m/s, drift max {dist.max():4.2f} m, bank {w.phi.min():+5.1f}..{w.phi.max():+5.1f}"
w=b[b.t>25]; e=b[b.t>58]
print(f"{t:8s} turn: {seg(25,37.5)} | release: {seg(37.5,45)} | 3 s turn + release: {seg(45,60)} | height {w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f} | yaw: 90% at {(w[w.r.abs()>27].t.iloc[0]-25.1):.2f}s, stopped {(b[(b.t>37.6)&(b.r.abs()<2)].t.iloc[0]-37.6):.2f}s | end drift {np.hypot(e.pos_x.mean()-x0,e.pos_z.mean()-z0):.2f} m")
PY
