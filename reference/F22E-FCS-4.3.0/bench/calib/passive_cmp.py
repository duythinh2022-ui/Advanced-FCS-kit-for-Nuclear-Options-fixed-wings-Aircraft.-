#!/usr/bin/env python3
# 2.31.2: the FCS passive evaluator (AirframeMomentAt) vs the plant, per AoA band, on sliptruth outputs.
# Usage: passive_cmp.py <sliptruth csv...>   (bench sliptruth log=<csv> out=<csv> every=5 [MisStab=1 MisRud=1 MisWing=1 MisBody=1 DCnbHi=0])
import sys,numpy as np,pandas as pd
d=pd.concat([pd.read_csv(f) for f in sys.argv[1:]]); d=d[d.tas>60]
a=np.radians(d.alpha); ca,sa=np.cos(a),np.sin(a)
d["kP"]=d.v0N*ca-d.v0L*sa; d["kE"]=d.eNb*ca-d.eLb*sa; d["kQ"]=d.prN*ca-d.prL*sa; d["k3"]=d.v3N*ca-d.v3L*sa
bands=[(-60,-40),(-40,-20),(-20,0),(0,10),(10,20),(20,30),(30,40),(40,50),(50,60),(60,70)]
print("medians at flight qbar. static: deg/s2 per deg beta; rate: per deg/s.  P = plant (bench truth), P3 = plant w/o bench-only multipliers, E = new FCS evaluator, Q = old probe")
print(" band     n |   Lb P/P3/E/Q            |   Nb P/P3/E/Q          |  kB P/P3/E/Q            |  Lr P/E     Lp P/E     Nr P/E     Np P/E")
for lo,hi in bands:
    m=d[(d.alpha>=lo)&(d.alpha<hi)]
    if len(m)<5: continue
    md=lambda c: m[c].median()
    print(f"{lo:4d}..{hi:<3d}{len(m):5d} | {md('v0L'):5.2f} {md('v3L'):5.2f} {md('eLb'):5.2f} {md('prL'):5.2f} | {md('v0N'):5.2f} {md('v3N'):5.2f} {md('eNb'):5.2f} {md('prN'):5.2f} | {md('kP'):5.2f} {md('k3'):5.2f} {md('kE'):5.2f} {md('kQ'):5.2f} | {md('Lr'):5.2f}/{md('eLr'):5.2f} {md('Lp'):5.2f}/{md('eLp'):5.2f} {md('Nr'):5.2f}/{md('eNr'):5.2f} {md('Np'):5.2f}/{md('eNp'):5.2f}")
print()
print("present-state passive accel, E vs plant aero (no thrust): RMS / median abs error, deg/s2 (and plant std for scale)")
for lo,hi in bands:
    m=d[(d.alpha>=lo)&(d.alpha<hi)]
    if len(m)<5: continue
    ex=m.eAX-m.pAX; ez=m.eAZ-m.pAZ
    print(f"{lo:4d}..{hi:<3d} roll {np.sqrt((ex**2).mean()):6.1f} {ex.abs().median():5.1f} (std {m.pAX.std():5.1f})   yaw {np.sqrt((ez**2).mean()):5.1f} {ez.abs().median():5.1f} (std {m.pAZ.std():5.1f})")
