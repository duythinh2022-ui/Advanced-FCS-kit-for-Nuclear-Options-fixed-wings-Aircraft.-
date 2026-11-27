#!/bin/bash
# stovlall regression + summary. usage: regr.sh <tag>   (BENCH_BIN picks the build)
tag=$1; cd /home/claude/vortex; ./stovlall.sh >/dev/null 2>&1; rm -rf /tmp/chk_$tag; cp -r /tmp/chk /tmp/chk_$tag
python3 - $tag <<'PY'
import sys,glob,re,pandas as pd,numpy as np
o=f'/tmp/chk_{sys.argv[1]}'
print('==',sys.argv[1])
for n in ['vl_b1188','vl_b135','vl_b15','vl_s1188','vl_sdef']:
    d=pd.read_csv(f'{o}/{n}.csv'); log=open(f'{o}/{n}.csv.log').read()
    g=re.search(r'gear: max compression per leg ([\d. ]+)',log); brk='BREAK' in log
    w=d[(d.t>24)]; lo=w[w.alt<w.alt.min()+0.3]; td=w[w.t<lo.t.iloc[0]].vs.iloc[-30:].min() if len(lo) else float('nan')
    print(f"  {n:9s} touchdown sink {td:.2f} m/s, gear {g.group(1).strip() if g else '?'}{' GEAR BREAK' if brk else ''}, hover vs before push {d[(d.t>18)&(d.t<24)].vs.min():+.2f}/{d[(d.t>18)&(d.t<24)].vs.max():+.2f}")
for n in ['h_s','h_b','h_s1288','h_b15']:
    d=pd.read_csv(f'{o}/{n}.csv'); w=d[(d.t>=20)]; h0=d[(d.t>18.5)&(d.t<20)].alt.mean()
    r=d[(d.t>=20)&(d.t<32)]; rp=r.r.abs().max(); t90=r[r.r.abs()>0.9*rp].t.iloc[0]-20.2; ts=r[(r.t>23.2)&(r.r.abs()<2)].t.min()-23.2
    print(f"  {n:8s} hover pedal/crab: vs {w.vs.min():+.2f}/{w.vs.max():+.2f}, height {w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f} m, yaw peak {rp:.0f} deg/s, 90% at {t90:.2f}s, stopped {ts:.2f}s after release, alt min {d[d.t>16].alt.min():.1f}")
d=pd.read_csv(f'{o}/v1.csv'); w=d[(d.t>5.5)&(d.t<10)]; print(f"  v1 VTO (lever to detent): vs min after lift-off {d[(d.t>8)&(d.t<10)].vs.min():+.2f}; climb release 16.3: vs after {d[(d.t>17)&(d.t<30)].vs.min():+.2f}/{d[(d.t>19)&(d.t<30)].vs.max():+.2f}; sink release 48.3: {d[(d.t>50)].vs.min():+.2f}/{d[(d.t>50)].vs.max():+.2f}, alt min {d[d.t>12].alt.min():.1f}")
d=pd.read_csv(f'{o}/v2.csv'); print(f"  v2 full-throttle STO: alt min after 8 s {d[d.t>8].alt.min():.2f}, final gs {d.gs.iloc[-1]:.0f} kt, vs {d[d.t>14].vs.min():+.2f}/{d[d.t>14].vs.max():+.2f}")
d=pd.read_csv(f'{o}/s3.csv'); on=d[d.state==2]; print(f"  s3 mission: STOVL-on bank max {on.phi.abs().max():.0f}, pitch {on.theta.min():.0f}..{on.theta.max():.0f}, alt {on.alt.min():.0f}..{on.alt.max():.0f}, final ias {d.ias.iloc[-1]:.0f} km/h state {int(d.state.iloc[-1])}, beta max {on[on.ias>20].beta.abs().max():.0f}")
PY
