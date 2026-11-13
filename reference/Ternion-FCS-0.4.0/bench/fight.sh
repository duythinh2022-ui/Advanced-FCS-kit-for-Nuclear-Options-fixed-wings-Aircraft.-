#!/bin/bash
# usage: fight.sh tag [overrides...]
tag=$1; shift
timeout 900 dotnet ${BIN:-bin}/bench.dll one iasKmh=${IAS:-340} pull=0 hold=0.1 roll=${ROLL:-0} yaw=${YAW:-1} rollT=1.5 post=2 throttle=0.6 ${DETACH:+detach=$DETACH} "$@" csv=runs/f_$tag.csv >/dev/null 2>&1
python3 - "$tag" <<'PY'
import sys,pandas as pd,numpy as np
t=sys.argv[1]; d=pd.read_csv(f'/home/claude/bench/runs/f_{t}.csv'); w=d[(d.t>0)&(d.t<1.5)]
S=w.cmdX_sL+w.cmdX_sR; A=w.cmdX_ailL+w.cmdX_ailR; F=w.cmdX_flapL+w.cmdX_flapR
f=((S.abs()+A.abs()+F.abs())-(S+A+F).abs()).values/2
print(f"{t:28s} fight mean {f.mean():6.1f} max {f.max():6.1f} | stabdiff {(w.stabL-w.stabR).abs().mean():5.1f} aildiff {(w.ailL-w.ailR).abs().mean():5.1f} flapdiff {(w.flapL-w.flapR).abs().mean():5.1f} | p-pCmd rms {np.sqrt(((w.p-w.pCmd)**2).mean()):5.2f} |p|max {w.p.abs().max():5.1f} beta {w.beta.abs().max():5.1f} r {w.r.abs().max():5.1f} rud {w.rudL.abs().max():4.1f} yawErr rms {np.sqrt(((w.r-w.rCmd)**2).mean()):5.2f}")
PY
