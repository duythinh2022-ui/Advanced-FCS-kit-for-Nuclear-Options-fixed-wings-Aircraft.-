#!/bin/bash
# lopsided hover with lateral stick: one of two stores comes off at 25 s (left one: jet is right-heavy), then right stick 2.4 s (toward the heavy side),
# 2.5 s free, left stick 4.8 s, free (02:55 flight). usage: lop.sh <tag> <kg each> <part stem> <mass without stores> [args]
tag=$1; kg=$2; part=$3; m=$4; shift 4
ROLL=${ROLL:-"roll=0:0,36:0,36.1:1,38.5:1,38.6:0,41:0,41.1:-1,45.9:-1,46:0"}
timeout 900 /home/claude/vortex/b.sh stovl ground=1 ias=0 T=${TT:-70} tog=0.5 "thr=0:0,5:0,5.5:0.20" "pitch=0:0,8:0,8.3:0.6,13:0.6,13.3:0" "$ROLL" print=100 drop=25,$kg,${part}_L csv=/tmp/lg/lop_$tag.csv mass=$m ThrustScale=1.12 DragScale=0.885 StoreKg=$kg StorePart=${part}_L+${part}_R "$@" > /tmp/lg/lop_$tag.txt 2>&1
python3 - $tag <<'PY'
import sys,pandas as pd,numpy as np
tag=sys.argv[1]; d=pd.read_csv(f'/tmp/lg/lop_{tag}.csv')
h=d[(d.t>31)&(d.t<35.5)]; w=d[d.t>=36]; h0=h.alt.mean()
e=d[d.t>d.t.max()-3]
crash=(w.phi.abs().max()>80) or (w.alt.min()<2.3)
back=w[(w.t>46)&(w.phi.abs()<2)&(w.p.abs()<2)&(w.gs<1.94)]
print(f"{tag:10s} hover post {h.postIn.mean():+.3f} | stick: bank {w.phi.min():+6.1f}..{w.phi.max():+6.1f} deg, p {w.p.min():+6.1f}..{w.p.max():+6.1f}, post at stop {((w.postIn.abs()>0.995).mean()*100):3.0f} % | skid peak {w.gs.max()/1.944:5.1f} m/s | height {w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f} m, vs {w.vs.min():+.2f}/{w.vs.max():+.2f} | level+stopped at {(back.t.iloc[0] if len(back) else float('nan')):.1f} s | end: bank {e.phi.mean():+.1f}, gs {e.gs.mean()/1.944:.1f} m/s {'FLIP/CRASH' if crash else ''}")
PY
