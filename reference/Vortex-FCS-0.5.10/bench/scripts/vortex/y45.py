import pandas as pd, numpy as np, sys
t_in,t_out=float(sys.argv[2]),float(sys.argv[3])
for n in sys.argv[1].split(','):
    d=pd.read_csv('/tmp/%s.csv'%n); w=d[(d.t>t_in)&(d.t<t_out)]; x=d[(d.t>t_out)&(d.t<t_out+10)]
    psi=np.unwrap(np.radians(d.psi))*57.3; i40=d[d.t>=t_out].index[0]; hz=1/(d.t.iloc[1]-d.t.iloc[0])
    print(n,"entry: skid max %.2f m/s (beta %.1f), u %.2f..%.2f, alt %.2f..%.2f, yaw rate max %.1f, end %.1f | exit: skid %.2f..%.2f, bank max %+.1f, heading after release %.1f deg, alt %.2f..%.2f | post sat %.2f s, mass %.0f"%(w.vSide.abs().max(), w.beta.abs().max(), w.uFwd.min(), w.uFwd.max(), w.alt.min(), w.alt.max(), w.psiRate.abs().max(), w.psiRate.iloc[-1], x.vSide.min(), x.vSide.max(), x.phi.max(), abs(psi[-1]-psi[i40]), x.alt.min(), x.alt.max(), (d[(d.t>t_in)].postIn.abs()>0.98).sum()/hz, d.mass.iloc[-1]))
