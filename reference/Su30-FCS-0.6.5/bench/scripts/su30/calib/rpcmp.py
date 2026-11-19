import sys,pandas as pd,numpy as np
g=pd.read_csv(sys.argv[1],low_memory=False); 
cols=['alpha','q','p','r','ias','beta','tvcNow','elevator_L_now','elevator_R_now','canard_L_now','canard_R_now','tvcdNow']
for f in sys.argv[2:]:
    b=pd.read_csv(f,low_memory=False)
    print('==',f); gw=g[(g.t>=b.t.min())&(g.t<=b.t.max())]
    for t in np.arange(b.t.min(),b.t.max()+.01,0.4):
        gi=gw.iloc[(gw.t-t).abs().argmin()]; bi=b.iloc[(b.t-t).abs().argmin()]
        print(f"t={t:6.1f} AoA g {gi.alpha:5.1f} b {bi.alpha:5.1f} | q g {gi.q:6.1f} b {bi.q:6.1f} | p g {gi.p:6.1f} b {bi.p:6.1f} | r g {gi.r:6.1f} b {bi.r:6.1f} | ias g {gi.ias*3.6:4.0f} b {bi.ias*3.6:4.0f} | beta g {gi.beta:5.1f} b {bi.beta:5.1f} | stab g {gi.elevator_L_now:4.0f}/{gi.elevator_R_now:4.0f} b {bi.elevator_L_now:4.0f}/{bi.elevator_R_now:4.0f} | can g {gi.canard_L_now:4.0f} b {bi.canard_L_now:4.0f} | tvc g {gi.tvcNow:5.1f} b {bi.tvcNow:5.1f}")
    for c in ['alpha','q','p','r','beta','ias']:
        gi=np.interp(b.t,gw.t,gw[c]); e=b[c]-gi
        print(f"   {c:6s} rms diff {np.sqrt((e**2).mean()):.2f}")
