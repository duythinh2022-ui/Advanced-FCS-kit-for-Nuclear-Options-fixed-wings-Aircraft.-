#!/bin/bash
# hover with a lopsided load: VTO, hover, pedal 3 s, crab 5 s. usage: asym.sh <tag> <storeKg> <part> [args]
tag=$1; kg=$2; part=$3; shift 3
timeout 600 /home/claude/vortex/b.sh stovl ground=1 ias=0 T=50 tog=0.5 "thr=0:0,5:0,5.5:0.20" "pitch=0:0,8:0,8.3:0.6,13:0.6,13.3:0" "yaw=0:0,22:0,22.2:1,25:1,25.2:0" "roll=0:0,32:0,32.2:1,37:1,37.2:0" print=100 dump=20 csv=/tmp/lg/asym_$tag.csv mass=13800 ThrustScale=1.12 DragScale=0.885 StoreKg=$kg StorePart=$part "$@" > /tmp/lg/asym_$tag.txt 2>&1
python3 - $tag <<'PY'
import sys,re,pandas as pd,numpy as np
tag=sys.argv[1]; txt=open(f'/tmp/lg/asym_{tag}.txt').read()
g=re.search(r'trim needed (\d+) N m, limit (\d+) \(hover estimate\) vs (\d+)',txt); 
d=pd.read_csv(f'/tmp/lg/asym_{tag}.csv'); air=d[d.alt>2.5]
h=d[(d.t>16)&(d.t<22)]; post=(h.postL-h.postR).mean(); pmax=max(d.postL.max(),d.postR.max())
w=d[d.t>14]
need=float(g.group(1)) if g else float('nan'); full=4*float(g.group(3)) if g else float('nan')
print(f"{tag:14s} trim {need:6.0f} N m = {100*need/full if full else 0:3.0f} % of post authority | hover post use {abs(post)/max(1,pmax)*100:3.0f} % | bank {w.phi.min():+6.1f}..{w.phi.max():+6.1f}, roll rate max {w.p.abs().max():5.1f}, alt {w.alt.min():5.1f}..{w.alt.max():5.1f}, vs {w.vs.min():+.2f}/{w.vs.max():+.2f}, final gs {d.gs.iloc[-1]:.1f} kt {'CRASH/FLIP' if (w.phi.abs().max()>60 or w.alt.min()<2.2) else ''}")
PY
