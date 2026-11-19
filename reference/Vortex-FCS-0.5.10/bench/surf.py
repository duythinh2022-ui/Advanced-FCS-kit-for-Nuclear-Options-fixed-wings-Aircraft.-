import sys,pandas as pd,numpy as np,warnings
warnings.filterwarnings('ignore')
for t in sys.argv[1:]:
    d=pd.read_csv(f'/home/claude/bench/runs/h_{t}.csv'); d=d[d.t<-0.02]
    w0=d[d.sr.abs()>0.5]; t0,t1=w0.t.iloc[0],w0.t.iloc[-1]
    w=d[(d.t>=t0)&(d.t<=t1+1.5)]; pre=d[(d.t>t0-0.5)&(d.t<t0)]
    S=w.cmdX_sL+w.cmdX_sR; A=w.cmdX_ailL+w.cmdX_ailR; F=w.cmdX_flapL+w.cmdX_flapR
    fi=((S.abs()+A.abs()+F.abs())-(S+A+F).abs())/2
    la=np.maximum(w.sL_laoa.abs(),w.sR_laoa.abs())
    ps=d.p*np.cos(np.radians(d.alpha))+d.r*np.sin(np.radians(d.alpha)); dt=d.t.diff().median()
    mu=(ps.where(d.t>=t0,0)).cumsum()*dt; mu1=mu[(d.t-(t0+1)).abs().idxmin()]
    print(f"{t:18s} a {pre.alpha.mean():5.1f} ias {pre.ias.mean():4.0f} | roll@1s {mu1:6.0f} | stab |localAoA| max {la.max():5.1f} time>35 {(la>35).mean()*100:3.0f}% | tvc pre {pre.tvc.mean():5.1f} min {w.tvc.min():6.1f} mean {w.tvc.mean():6.1f} | fight {fi.mean():5.1f}/{fi.max():5.1f} | over {(w.alpha-w.alphaCmd).max():4.1f} q-qT {(w.q-w.qT).min():5.1f}..{(w.q-w.qT).max():4.1f}")
