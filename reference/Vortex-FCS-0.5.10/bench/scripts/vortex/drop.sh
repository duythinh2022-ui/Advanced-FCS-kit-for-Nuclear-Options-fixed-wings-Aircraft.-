#!/bin/bash
# hover, then one of two symmetric stores comes off. usage: drop.sh <tag> <kg each> <part stem e.g. wing2b> <mass without stores> [args]
tag=$1; kg=$2; part=$3; m=$4; shift 4
timeout 600 /home/claude/vortex/b.sh stovl ground=1 ias=0 T=50 tog=0.5 "thr=0:0,5:0,5.5:0.20" "pitch=0:0,8:0,8.3:0.6,13:0.6,13.3:0" print=100 drop=${DROPT:-25},$kg,${part}_L csv=/tmp/lg/drop_$tag.csv mass=$m ThrustScale=1.12 DragScale=0.885 StoreKg=$kg StorePart=${part}_L+${part}_R "$@" > /tmp/lg/drop_$tag.txt 2>&1
python3 - $tag <<'PY'
import sys,pandas as pd,numpy as np
tag=sys.argv[1]; d=pd.read_csv(f'/tmp/lg/drop_{tag}.csv'); import os; T0=float(os.environ.get("DROPT","25")); w=d[d.t>=T0]; h0=d[(d.t>T0-2)&(d.t<T0)].alt.mean()
pmax=max(d.postL.max(),d.postR.max()); pu=np.maximum(w.postL,w.postR)/max(1,pmax)
t1=w[pu>0.95].t; back=w[(w.t>T0+1)&(w.phi.abs()<2)&(w.p.abs()<2)&(w.gs<1.94)]
print(f"{tag:12s} thr before {d[(d.t>T0-0.3)&(d.t<T0)].thrEng.mean():.2f} | bank peak {w.phi.abs().max():5.1f} deg at +{w.t[w.phi.abs().idxmax()]-T0:.1f} s, roll rate peak {w.p.abs().max():5.1f} | skid peak {w.gs.max()/1.944:5.1f} m/s | height {w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f} m, vs {w.vs.min():+.2f}/{w.vs.max():+.2f} | post full for {len(t1)*0.0167:.1f} s, first full at +{(t1.iloc[0]-T0) if len(t1) else float('nan'):.2f} s, steady post {pu[w.t>45].mean()*100:.0f} % | level and stopped at +{(back.t.iloc[0]-T0) if len(back) else float('nan'):.1f} s {'FLIP/CRASH' if (w.phi.abs().max()>80 or w.alt.min()<2.3) else ''}")
PY
