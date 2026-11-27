#!/bin/bash
# ckr.sh <variant> <tag> [Name=value...]: corkscrew push-overs: full aft + full roll from 0.3 s, at 5 s push full;
# CKR = roll released at the push, CKN = roll held through the push. 250 / 350 km/h starts.
source /home/claude/scripts/penv.sh; penv $1; v=$1; tag=$2; shift 2
o=/home/claude/x/proxy/ckr_${v}_$tag; mkdir -p $o; cd $BENCH_DIR
for ias in 250 350; do
  dotnet bin/bench.dll ifly ias=$ias alt=2500 thr=1 T=9 pitch=0:0,0.05:1,5:1,5.08:-1,9:-1 roll=0:0,0.3:0,0.35:1,5:1,5.05:0,9:0 fcslog=$o/ckr$ias.csv "$@" > /dev/null 2>&1 &
  dotnet bin/bench.dll ifly ias=$ias alt=2500 thr=1 T=9 pitch=0:0,0.05:1,5:1,5.08:-1,9:-1 roll=0:0,0.3:0,0.35:1,9:1 fcslog=$o/ckn$ias.csv "$@" > /dev/null 2>&1; wait
done
python3 - $o <<'PY'
import sys,glob,numpy as np,pandas as pd
for f in sorted(glob.glob(sys.argv[1]+'/*.csv')):
    d=pd.read_csv(f); w=d[d.t>=5]; a0=d[(d.t>4.5)&(d.t<5)].alpha.mean()
    t10=(w[w.alpha<10].t.min()-5) if (w.alpha<10).any() else float('nan')
    ww=w[w.t<5+(t10 if t10==t10 else 3)]
    q=ww.q.values; dq=np.diff(q)
    # dips: the push rate (negative q) easing off by more than 3 deg/s before the bottom
    run=np.minimum.accumulate(q); dip=(q-run).max() if len(q) else 0
    r5=d[(d.t>4.8)&(d.t<5)].r.abs().mean()
    print(f"{f.split('/')[-1]:10s} a0 {a0:5.1f} r@push {r5:5.1f} | t->10deg {t10:4.2f}s  betaPk {ww.beta.abs().max():5.1f}  qMin {q.min() if len(q) else 0:6.1f}  qDip {dip:4.1f}  yawShedK {d.yawShedK.iloc[-1]:.2f}")
PY
