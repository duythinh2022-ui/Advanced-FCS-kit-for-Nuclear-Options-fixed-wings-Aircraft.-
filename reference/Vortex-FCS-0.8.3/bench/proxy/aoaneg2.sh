#!/bin/bash
# aoaneg.sh <variant> <tag> [Name=value...]: full push from 200-500 km/h, full roll left 3-11 s, right 11-19 s, release at 20 s
source /home/claude/scripts/penv.sh; penv $1; v=$1; tag=$2; shift 2
o=/home/claude/x/proxy/aoaneg_${v}_$tag; mkdir -p $o; cd $BENCH_DIR
for ias in ${IASL:-200 300 400 500}; do
  dotnet bin/bench.dll ifly ias=$ias alt=2500 thr=${THR:-1} T=24 pitch=0:0,0.05:-1,20:-1,20.1:0 roll=0:0,3:0,3.05:-1,11:-1,11.1:1,19:1,19.05:0,24:0 fcslog=$o/i$ias.csv "$@" > $o/i$ias.log 2>&1 &
  
done; wait
python3 - $o <<'PY'
import sys,glob,numpy as np,pandas as pd
for f in sorted(glob.glob(sys.argv[1]+'/i*.csv'),key=lambda s:int(s.split('/i')[-1][:-4])):
    d=pd.read_csv(f); ias=f.split('/i')[-1][:-4]
    ph=lambda a,b:d[(d.t>=a)&(d.t<b)]; L,R,Re=ph(4,11),ph(12,19),ph(20,24)
    pS=lambda w:(w.p*np.cos(np.radians(w.alpha))+w.r*np.sin(np.radians(w.alpha)))
    t10=(Re[Re.alpha>-10].t.min()-20) if (Re.alpha>-10).any() else float('nan')
    print(f"{ias:>4} | rollL a {L.alpha.mean():6.1f} b {L.beta.abs().max():5.1f} vv {pS(L).abs().mean():5.1f} | rollR a {R.alpha.mean():6.1f} b {R.beta.abs().max():5.1f} vv {pS(R).abs().mean():5.1f} | rel t->-10 {t10:4.2f} | aMin {d.alpha.min():6.1f} bMax {d.beta.abs().max():5.1f}")
PY
