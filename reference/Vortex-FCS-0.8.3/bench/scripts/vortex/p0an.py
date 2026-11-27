import sys, pandas as pd, numpy as np
o=sys.argv[1]
def lp(x,tau,dt):
    y=np.empty_like(x); y[0]=x[0]; a=dt/(tau+dt) if tau>0 else 1.0
    for i in range(1,len(x)): y[i]=y[i-1]+a*(x[i]-y[i-1])
    return y
def fit(d,cmd,rate,t0,t1,lab):
    w=d[(d.t>=t0)&(d.t<t1)]; c=w[cmd].values.astype(float); r=w[rate].values.astype(float); dt=float(np.median(np.diff(w.t.values)))
    best=(1e9,0,0)
    for k in range(0,9):
        cs=np.concatenate([np.full(k,c[0]),c[:len(c)-k]]) if k>0 else c
        for tau in np.arange(0,0.8,0.01):
            e=np.sqrt(np.mean((lp(cs,tau,dt)-r)**2))
            if e<best[0]: best=(e,tau,k*dt)
    # pure first-order (no delay)
    b1=min(((np.sqrt(np.mean((lp(c,tau,dt)-r)**2)),tau) for tau in np.arange(0,0.8,0.01)))
    amp=np.abs(c).max()
    print(f"  {lab:28s} cmd peak {amp:5.1f} deg/s | lag+delay fit: tau {best[1]:.2f} s + delay {best[2]:.3f} s = {best[1]+best[2]:.2f} s (rms {best[0]:.2f}) | first-order only: tau {b1[1]:.2f} s (rms {b1[0]:.2f})")
    return best[1]+best[2], b1[1]
H=pd.read_csv(f'{o}/H.csv'); M=pd.read_csv(f'{o}/M.csv'); D=pd.read_csv(f'{o}/D.csv'); S=pd.read_csv(f'{o}/S.csv')
print('== command-to-rate lag (inner loops)')
tp,_=fit(H,'pCmd','p',20,30,'hover roll (side stick 5 s)')
fit(H,'pCmd','p',40,46,'hover roll (tap)')
tr,_=fit(H,'rCmd','r',50,58,'hover yaw (pedal 3 s)')
tq,_=fit(H,'qCmd','q',65,90,'hover pitch (lever step)')
w=M[(M.t>38)&(M.t<40)]
print(f"  M case speed before inputs: gs {w.gs.mean():.1f} kt, ias {w.ias.mean():.0f} km/h, lamV {w.lamV.mean():.2f} lamT {w.lamT.mean():.2f} wC {w.wC.mean():.2f}")
tpm,_=fit(M,'pCmd','p',40,56,'30 kt roll (bank stick)')
trm,_=fit(M,'rCmd','r',58,66,'30 kt yaw (pedal)')
tqm,_=fit(M,'qCmd','q',70,82,'30 kt pitch (lever step)')
print('== today\'s gain x measured lag (0.5 = the damping rule)')
for lab,k,t in (('bank 2.5/s',2.5,tp),('pitch 2/s',2.0,tq),('heading 2.5/s',2.5,tr),('bank 2.5/s @30kt',2.5,tpm),('pitch 2/s @30kt',2.0,tqm),('sideslip 1.5/s (yaw) @30kt',1.5,trm)):
    print(f"  {lab:28s} x {t:.2f} s = {k*t:.2f}   -> LoopGain = 1/(2 tau) = {1/(2*max(t,1e-3)):.2f} /s  ({(1/(2*max(t,1e-3))/k-1)*100:+.0f} % vs today)")
print('== reference hover (t 17-19.5)')
w=H[(H.t>17)&(H.t<19.5)]
for c in ['mass','alt','aHlevel','aVres','aUpBody','aTiltLeft','authP','authQ','authR','tauV','kRollS','rollWeak','thrEng','swivel','theta']:
    print(f"  {c:10s} {w[c].mean():8.3f}")
print(f"  T_trc for 15 kt = 7.72 / aHlevel = {7.72/w.aHlevel.mean():.2f} s ; T_tilt for 10 deg/s: roll {10/ (w.authP.mean()*w.kRollS.mean()):.3f} s, pitch {10/w.authQ.mean():.3f} s")
print(f"  a_tilt / a_upBody = {w.aTiltLeft.mean()/max(1e-3,w.aUpBody.mean()):.2f}  (today's 1 : 0.25 weights mean 2.0)")
print('== side translation (full stick 20.1-25.1, release)')
w=H[(H.t>=20)&(H.t<40)]; dt=np.median(np.diff(w.t)); h0=H[(H.t>18.5)&(H.t<20)].alt.mean()
ph=np.gradient(w.phi.values,dt)
v=w.vSide.values*1.944
print(f"  bank rate peak in {np.abs(ph[w.t.values<25]).max():.1f} deg/s (cmd rate {np.abs(np.gradient(w.phiTrc.values,dt)).max():.1f}), bank max {w.phi.abs().max():.1f} deg, speed max {np.abs(v).max():.1f} kt, at 25.1 s {abs(v[np.argmin(abs(w.t.values-25.1))]):.1f} kt")
aft=w[w.t>25.1]; ts=aft[(aft.vSide.abs()<0.3)].t.min()-25.1; ov=-(np.sign(v[np.argmax(np.abs(v))])*aft.vSide).min()
print(f"  stop to 0.3 m/s {ts:.2f} s after release, overshoot {max(0,ov):.2f} m/s, height {w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f} m, vs {w.vs.min():+.2f}/{w.vs.max():+.2f}")
print('== fore-aft (lever 0.35 at 65.1, back at 78.1)')
w=H[(H.t>=65)&(H.t<100)]; h0=H[(H.t>63)&(H.t<65)].alt.mean()
aft=w[w.t>78.1]; ts=aft[(aft.uFwd.abs()<0.3)].t.min()-78.1
print(f"  speed max {w.uFwd.max()*1.944:.1f} kt (cmd {w.gsCmd.max()*1.944:.1f}), pitch {w.theta.min():.1f}..{w.theta.max():.1f}, thetaAssist {w.thetaAssist.min():.1f}..{w.thetaAssist.max():.1f}, pitch-rate peak {w.q.abs().max():.1f}, stop to 0.3 m/s {ts:.2f} s, back-run {min(0,aft.uFwd.min()):.2f} m/s, height {w.alt.min()-h0:+.2f}/{w.alt.max()-h0:+.2f}")
print('== shares along the decel (D) and the short take-off (S), reference')
def at(d,col,v,key='ias'):
    i=(d[key]-v).abs().values.argmin(); return d.iloc[i]
on=D[D.state==2]
print('  D: speed (m/s TAS~) -> lamL, lamB(slip), slipStiff, authR, aBrakeFull, resZ, abAuto, wC')
for vv in (10,15,20,25,30,45,60):
    r=at(on,'ias',vv*3.6); print(f"    {vv:3d} m/s: lamL {r.lamL:.3f} lamB {r.lamB:.3f} stiff {r.slipStiff:.3f} aYaw {r.authR:5.1f} aBrakeFull {r.aBrakeFull:.3f} resZ {r.resZ:+.2f} abAuto {r.abAuto:.2f} wC {r.wC:.2f} theta {r.theta:.1f} alpha {r.alpha:.1f}")
on=S[S.state==2]
print('  S (STO):')
for vv in (10,15,20,25,30,45):
    r=at(on,'ias',vv*3.6); print(f"    {vv:3d} m/s: lamL {r.lamL:.3f} lamB {r.lamB:.3f} stiff {r.slipStiff:.3f} aYaw {r.authR:5.1f} wC {r.wC:.2f} alt {r.alt:.1f}")
