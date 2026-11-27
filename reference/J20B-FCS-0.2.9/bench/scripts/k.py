import pandas as pd,numpy as np,sys
from scipy.signal import find_peaks
d=pd.read_csv(sys.argv[1]); t0=float(sys.argv[2]); s=d[d.t>t0]
b=s.beta.values; t=s.t.values; dt=np.diff(t).mean()
pk,_=find_peaks(np.abs(b),distance=10); pv=[abs(b[i]) for i in pk if abs(b[i])>0.15]
first=pv[0] if pv else 0; os_=(pv[1]/pv[0]) if len(pv)>1 else 0
late=s[s.t>t0+2.5]
print(f"{np.abs(b).max():4.1f} os {os_:4.2f} n {len(pv)} iae {np.abs(b).sum()*dt:5.2f} lateRMS {np.sqrt((late.beta**2).mean()):4.2f} pLate {late.p.std():4.2f} aMin {s.alpha.min():4.1f}")
