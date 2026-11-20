import pandas as pd, numpy as np, sys
def nrev(x, thr):
    x=np.asarray(x,float); e=x[0]; d=0; n=0
    for v in x[1:]:
        if d>=0 and v<e-thr: n+=(d>0); d=-1; e=v
        elif d<=0 and v>e+thr: n+=(d<0); d=1; e=v
        elif (d>=0 and v>e) or (d<=0 and v<e): e=v
    return n
import os
jets=[j for j in ['bufref','stock','buflight','stk128','buf136','bufasym'] if os.path.exists(f'/tmp/b50/{j}_on/D.csv') and os.path.exists(f'/tmp/b50/{j}_off/D.csv')]
for j in jets:
    print("==",j)
    for k in ['off','on']:
        H=pd.read_csv(f'/tmp/b50/{j}_{k}/H.csv'); M=pd.read_csv(f'/tmp/b50/{j}_{k}/M.csv'); S=pd.read_csv(f'/tmp/b50/{j}_{k}/S.csv'); D=pd.read_csv(f'/tmp/b50/{j}_{k}/D.csv'); V=pd.read_csv(f'/tmp/b50/{j}_{k}/V.csv')
        w=H[(H.t>=65.1)&(H.t<78)]; cmd=w.gsCmd.max(); t90=w[w.uFwd>=0.9*cmd].t.min()-65.1; a=w[(w.uFwd>0.2*cmd)&(w.uFwd<0.7*cmd)]; acc=(a.uFwd.iloc[-1]-a.uFwd.iloc[0])/(a.t.iloc[-1]-a.t.iloc[0]); h0=H[(H.t>63)&(H.t<65)].alt.mean()
        ratio=(w.thrustReq/(w.tMx*1e-3))
        m=M[(M.t>=18.1)&(M.t<40)]; mc=m.gsCmd.max(); mt=m[m.uFwd>=0.9*mc].t.min()-18.1; b=m[(m.uFwd>0.2*mc)&(m.uFwd<0.7*mc)]; macc=(b.uFwd.iloc[-1]-b.uFwd.iloc[0])/(b.t.iloc[-1]-b.t.iloc[0]); mh=M[(M.t>16)&(M.t<18)].alt.mean()
        m2=M[(M.t>=40)]; 
        print(f"  {k:3s} H lever 7 m/s: accel {acc:.2f} m/s2, 90% in {t90:.2f}s, over {w.uFwd.max()-cmd:+.2f}, height {w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f}, thrust ratio max {ratio.max():.2f}, govLam<0.02 {(w.govLam<0.02).sum()/60:.2f}s | M lever {mc:.1f} m/s: accel {macc:.2f}, 90% in {mt:.2f}s, over {m.uFwd.max()-mc:+.2f}, height {m.alt.min()-mh:+.2f}/{m.alt.max()-mh:+.2f}, thetaA rev {nrev(m.thetaAssist.values,0.5)}, thr rev {nrev(m.thrEng.values,0.1)}, swivel rev {nrev(m.swivel.values,0.03)}")
        print(f"      M bank/pedal part (40-100 s): |phi| max {m2.phi.abs().max():.1f}, |beta| max {m2.beta.abs().max():.1f}, vSide max {m2.vSide.abs().max():.2f}, height {m2.alt.min()-M[(M.t>38)&(M.t<40)].alt.mean():+.2f}/{m2.alt.max()-M[(M.t>38)&(M.t<40)].alt.mean():+.2f}, r max {m2.r.abs().max():.1f}, phiCmd rev {nrev(m2.phiCmd.values,0.5)}, p rev {nrev(m2.p.values,1.0)}, post sat {(m2.postIn.abs()>0.98).sum()/60:.2f}s, u {m2.uFwd.min():.1f}..{m2.uFwd.max():.1f}")
        ws=H[(H.t>=78.1)&(H.t<90)]; ts=ws[ws.uFwd<0.3].t.min()-78.1; hs=H[(H.t>76)&(H.t<78)].alt.mean()
        vn=(f", vane {w.vaneNow.min():.1f}..{w.vaneNow.max():.1f}" if 'vaneNow' in H.columns else "")
        hv=H[(H.t>17)&(H.t<19.5)]
        print(f"      H hover: swivel {hv.swivel.mean():.3f}, spare {hv.aUpBody.mean():.2f}, aHlevel {hv.aHlevel.mean():.2f}, vmax {hv.trcVmax.mean()*1.944:.1f} kt | H stop from 7 m/s: <0.3 in {ts:.2f}s, past zero {max(0,-ws.uFwd.min()):.2f}, pitch max {ws.theta.max():.1f}, height {ws.alt.min()-hs:+.2f}/{ws.alt.max()-hs:+.2f}{vn} | M accel pitch {m.theta.min():.2f}..{m.theta.max():.2f}, thrust ratio max H {ratio.max():.2f}")
        print(f"      S: ias end {S.ias.iloc[-1]:.0f}, alt end {S.alt.iloc[-1]:.1f}, alt max {S.alt.max():.1f} | V: alt max {V.alt.max():.2f}, end {V.alt.iloc[-1]:.2f}, vs {V.vs.min():+.2f}/{V.vs.max():+.2f} | D: gs end {D.gs.iloc[-1]:.2f} kt, alt {D.alt.min():.1f}..{D.alt.max():.1f}, |phi| max {D.phi.abs().max():.1f}, t(gs<1kt) {D[D.gs<1].t.min():.1f}")
