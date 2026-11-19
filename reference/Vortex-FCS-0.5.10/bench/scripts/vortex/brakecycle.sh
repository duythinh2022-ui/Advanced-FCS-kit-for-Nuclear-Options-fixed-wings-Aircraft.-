#!/bin/bash
# Vortex 0.4.5: the airbrake set-cycling case (17 Nov 21:46 log, 190-206 s). usage: brakecycle.sh [Name=value ...]
# The log's condition (1125 km/h, 900 m, idle, 6.5 deg climb), brake opened at 2 s and held, stick centred, with a steady roll
# offset standing in for the game's roll mismatch with the brake out (rollExt, deg/s^2). 0.4.1-0.4.4: +25 banks left and switches
# sets; -25 is clean (the fight cost's phantom acted on one sign of the roll target only). 0.4.5: both clean.
R=$(cd "$(dirname "$0")/../../.."; pwd); O=${BENCH_OUT:-$R/bench/out}; mkdir -p "$O"
for e in 25 -25 0; do
  f="$O/brakecycle_$e.csv"
  bash "$R/bench.sh" fly ias=1125 alt=900 thr=0 T=16 gamma0=6.5 brake=2:0,2.05:1 rollExt=$e "fcslog=$f" "$@" >/dev/null 2>&1
  python3 - "$f" "$e" <<'PY'
import sys, warnings; warnings.simplefilter('ignore')
import pandas as pd, numpy as np
d = pd.read_csv(sys.argv[1], low_memory=False); w = d[d.t > 4]
s = (w.aileron_L_cmd + w.aileron_R_cmd) / 2; flips = int(np.abs(np.diff((s > 8).astype(int).values)).sum())
rev = 0
for c in ['elevator_L', 'elevator_R', 'aileron_L', 'aileron_R', 'rudder_L', 'rudder_R']:
    dc = np.diff(w[c + '_cmd'].values); big = dc[np.abs(dc) > 0.3]; rev += int(np.sum(np.sign(big[1:]) != np.sign(big[:-1]))) if len(big) > 1 else 0
print(f"rollExt {float(sys.argv[2]):+5.0f}: bank {d.phi.iloc[0]:5.1f} -> {d.phi.iloc[-1]:6.1f} deg | g {w.nz.min():.2f}..{w.nz.max():.2f} | roll rate {w.p.min():5.1f}..{w.p.max():4.1f} deg/s | "
      f"outer-elevon common {s.min():.1f}..{s.max():.1f} deg, set switches {flips} | roll target missed by > 10 deg/s2 {100 * (w.unR.abs() > 10).mean():.0f} % of the time | command reversals {rev}")
PY
done
