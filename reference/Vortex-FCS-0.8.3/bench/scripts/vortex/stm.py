import pandas as pd, numpy as np, sys, os
T0={'k128':33.1,'ref':33.1,'stock':33.1,'b136':33.1,'tom30':32.1,'tom45':35.1}
def nrev(x, thr):
    x=np.asarray(x,float); e=x[0]; d=0; n=0
    for v in x[1:]:
        if d>=0 and v<e-thr: n+=(d>0); d=-1; e=v
        elif d<=0 and v>e+thr: n+=(d<0); d=1; e=v
        elif (d>=0 and v>e) or (d<=0 and v<e): e=v
    return n
for tag in sys.argv[1:]:
    print('==',tag)
    for c,t0 in T0.items():
        f=f'/tmp/st/{tag}/{c}.csv'
        if not os.path.exists(f): continue
        d=pd.read_csv(f); w=d[(d.t>=t0-0.1)&(d.t<t0+14)]; u=w.uFwd.values; t=w.t.values-t0; h0=d[(d.t>t0-2)&(d.t<t0)].alt.mean()
        k=np.nonzero(np.abs(u)>0.3)[0]; t3=t[k[-1]] if len(k) else 0
        k=np.nonzero(np.abs(u)>0.15)[0]; t15=t[k[-1]] if len(k) else 0
        acc=np.gradient(u,t); first=t[np.nonzero(u<0.3)[0][0]]
        # deceleration curve shape: reversals of the acceleration > 0.4 m/s2, of pitch tilt, thrust, vane
        print(f"  {c:6s} from {u[0]*1.944:5.1f} kt: first <0.3 {first:5.2f}s, within 0.3 for good {t3:5.2f}s, within 0.15 {t15:5.2f}s, past zero {max(0,-u.min()):.2f} m/s, back-run {(-np.minimum(u,0)).sum()/60:.2f} m, distance {np.trapezoid(np.maximum(u,0),t):5.1f} m, peak decel {-acc.min():.2f}, height {w.alt.min()-h0:+.3f}/{w.alt.max()-h0:+.3f}, pitch max {w.theta.max():.1f}, thr max {w.thrEng.max():.2f}, gov<0.02 {(w.govLam<0.02).sum()/60:.2f}s, rev: acc>0.4 {nrev(acc,0.4)} thetaA>0.5 {nrev(w.thetaAssist.values,0.5)} thr>0.1 {nrev(w.thrEng.values,0.1)} vane>2 {nrev(w.vaneNow.values,2) if 'vaneNow' in w else 0} q>1 {nrev(w.q.values,1.0)}")
