#!/bin/bash
# held full-aft pull + roll. usage: hold.sh tag [overrides]
tag=$1; shift
timeout 900 dotnet ${BIN:-bin}/bench.dll one iasKmh=${IAS:-400} pull=${PULL:-1} hold=${HOLD:-6} rollFromPull=${RF:-2.5} roll=${ROLL:-1} rollT=${RT:-3} rollRev=${RREV:-0} post=1.5 throttle=${THR:-1} storeKg=${STORE:-0} "$@" csv=runs/h_$tag.csv > runs/h_$tag.out 2>&1
python3 - "$tag" <<'PY'
import sys,pandas as pd,numpy as np
t=sys.argv[1]; d=pd.read_csv(f'/home/claude/bench/runs/h_{t}.csv')
# t is relative to release; roll window
import re
tr=d.t.min()+0.5  # tPull
w=d[(d.sr.abs()>0.5)]
if len(w)==0: print(t,"no roll"); sys.exit()
t0,t1=w.t.iloc[0],w.t.iloc[-1]
pre=d[(d.t>t0-0.6)&(d.t<t0-0.05)]
ww=d[(d.t>=t0)&(d.t<=t1+0.8)]
a0=pre.alpha.mean(); ac=pre.alphaCmd.mean()
pinned=(ww.tvc<=ww.tvc.min()+0.3).mean() if ww.tvc.min()<-19 else 0
ov=(ww.alpha-ww.alphaCmd).max()
print(f"{t:26s} a0 {a0:5.1f} cmd {ac:5.1f}..{ww.alphaCmd.max():5.1f} | roll: aMax {ww.alpha.max():5.1f} over {ov:4.1f} aMin {ww.alpha.min():5.1f} |p|max {ww.p.abs().max():5.0f} phi@1s {d.iloc[(d.t-(t0+1)).abs().argmin()].phi:6.0f} | tvc min {ww.tvc.min():6.1f} mean {ww.tvc.mean():6.1f} pinned {pinned*100:3.0f}% | q-qT rms {np.sqrt(((ww.q-ww.qT)**2).mean()):5.2f} | asymCap {ww.asymCap.min():5.1f} ias {pre.ias.mean():4.0f}")
PY
