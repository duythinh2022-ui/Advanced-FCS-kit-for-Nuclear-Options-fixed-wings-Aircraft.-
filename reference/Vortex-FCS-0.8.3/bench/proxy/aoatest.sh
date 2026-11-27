#!/bin/bash
# aoatest.sh <variant> <tag> [Name=value...]: Tom's limit test (0.1.0 notes): full aft from 200-500 km/h, full roll left 3-11 s,
# right 11-19 s, full push 20-26 s; reports sideslip / AoA / roll per phase
source /home/claude/scripts/penv.sh; penv $1; v=$1; tag=$2; shift 2
o=/home/claude/x/proxy/aoa_$v_$tag; o=/home/claude/x/proxy/aoa_${v}_$tag; mkdir -p $o; cd $BENCH_DIR
for ias in 200 300 400 500; do
  dotnet bin/bench.dll ifly ias=$ias alt=1500 thr=1 T=27 pitch=0:0,0.05:1,20:1,20.1:-1,26:-1,26.1:0 roll=0:0,3:0,3.05:-1,11:-1,11.1:1,19:1,19.05:0,27:0 fcslog=$o/i$ias.csv "$@" > $o/i$ias.log 2>&1 &
  [ $ias = 300 ] && wait
done; wait
python3 - $o <<'PY'
import sys,glob,numpy as np,pandas as pd
o=sys.argv[1]
for f in sorted(glob.glob(o+'/i*.csv'),key=lambda s:int(s.split('/i')[-1][:-4])):
    d=pd.read_csv(f); ias=f.split('/i')[-1][:-4]
    def ph(a,b): return d[(d.t>=a)&(d.t<b)]
    L,R,Pu,Re=ph(4,11),ph(12,19),ph(20,26),ph(26,27)
    pS=lambda w:(w.p*np.cos(np.radians(w.alpha))+w.r*np.sin(np.radians(w.alpha)))
    t10=Pu[Pu.alpha<10].t.min()-20 if (Pu.alpha<10).any() else float('nan')
    print(f"{ias:>4} | rollL a {L.alpha.mean():5.1f} b {L.beta.abs().max():5.1f} vvroll {pS(L).abs().mean():5.1f} | rollR a {R.alpha.mean():5.1f} b {R.beta.abs().max():5.1f} vv {pS(R).abs().mean():5.1f} | push aMin {Pu.alpha.min():6.1f} b {Pu.beta.abs().max():5.1f} t10 {t10:4.2f} | aMax {d.alpha.max():5.1f} bMax {d.beta.abs().max():5.1f} iasMin {d.ias.min():4.0f}")
PY
