import pandas as pd,numpy as np,sys
d=pd.read_csv(sys.argv[1]); t0s=sys.argv[2:] 
def spec(w,c):
    x=w[c].values-w[c].values.mean(); n=len(x); dt=w.t.diff().median()
    f=np.fft.rfftfreq(n,dt); A=np.abs(np.fft.rfft(x*np.hanning(n)))*4/n
    m=f>0.6; i=np.argmax(A[m]); return f[m][i], A[m][i]
for t0 in np.arange(float(t0s[0]) if t0s else 10, d.t.max()-1, 4):
    w=d[(d.t>=t0)&(d.t<t0+4)]
    fp,ap=spec(w,'p'); fa,aa=spec(w,'ailL'); fe,ae=spec(w,'elevL'); fo,ao=spec(w,'postIn')
    print(f"{t0:5.0f} st{w.state.iloc[-1]} gs{w.gs.mean():5.1f}kt gsC{w.gsCmd.mean()*1.944:5.1f} phi{w.phi.mean():6.1f} r{w.r.mean():5.1f} beta{w.beta.mean():5.1f} yawIn{w.yawIn.mean():5.2f} | p {fp:4.2f}Hz ±{ap:4.2f} ail {fa:4.2f}Hz ±{aa:5.2f} elev ±{ae:5.2f} posts {fo:4.2f}Hz ±{ao:4.2f} surfW {w.surfW.mean():.2f}")
