#!/bin/bash
# CTOL store release, then a pull (09:33 log): your jet, 14.4 t with 500 kg on each inner (wing2a) and outer (wingtip) pylon;
# the left inner at 1 s, the left outer at 3 s, full aft stick from PULL (default 5.7 s) for 4 s. usage: ctdrop.sh <tag> [args]
tag=$1; shift
P=${PULL:-5.7}
timeout 600 /home/claude/vortex/b.sh ifly alt=${ALT:-250} ias=${IAS:-330} thr=${THR:-1} T=${TT:-14} "pitch=0:0,$P:0,$(python3 -c "print($P+0.15)"):1,$(python3 -c "print($P+4)"):1,$(python3 -c "print($P+4.15)"):0" ${ROLLP:+roll=$ROLLP} "drop=1,500,wing2a_L;3,500,wingtip_L" storeKg=500 storePart=wing2a_L+wing2a_R+wingtip_L+wingtip_R MassTarget=12436 ThrustScale=1.12 DragScale=0.885 csv=/tmp/lg/ct_$tag.csv fcslog=/tmp/lg/ctf_$tag.csv "$@" > /tmp/lg/ct_$tag.txt 2>&1
python3 - $tag $P <<'PY'
import sys,pandas as pd,numpy as np
tag=sys.argv[1]; P=float(sys.argv[2]); d=pd.read_csv(f'/tmp/lg/ctf_{tag}.csv')
w=d[(d.t>=P)&(d.t<P+6)]
dep=(w.r.abs().max()>40) or (w.p.abs().max()>90)
print(f"{tag:8s} cap at the pull: target {d[d.t>=P].asymCap.iloc[0]:5.1f}, effective {d[d.t>=P].asymCapEff.iloc[0]:5.1f} | AoA peak {w.alpha.max():5.1f} (cmd {w.alphaCmd.max():5.1f}), bank {w.phi.min():+6.1f}..{w.phi.max():+6.1f}, p {w.p.abs().max():5.1f}, r {w.r.abs().max():5.1f}, beta {w.beta.abs().max():4.1f}, nz max {w.nz.max():.2f}, ias {w.ias.min():.0f}..{w.ias.max():.0f} m/s {'DEPARTURE' if dep else ''}")
PY
