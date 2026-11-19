import sys,pandas as pd,numpy as np
for t in sys.argv[1:]:
    d=pd.read_csv(f'runs/{t}.csv'); dt=d.t.diff().median()
    w=d[d.yawIn.abs()>0.5]; w=w[w.t>w.t.iloc[0]+1.0]
    out=[]
    for c in ['stabL','stabR','flapL','ailL','rudL']:
        x=w[c].values; dx=np.diff(x); s=np.sign(dx[np.abs(dx)>0.05]); rev=(np.diff(s)!=0).sum()/(w.t.iloc[-1]-w.t.iloc[0])
        # amplitude of swings: mean abs of detrended (1s) signal
        hp=np.sqrt(np.mean((w[c]-w[c].rolling(int(1/dt),center=True).mean()).dropna()**2))
        out.append(f"{c} rev/s {rev:4.1f} amp {hp:4.1f}")
    rr=w.r; rhp=np.sqrt(np.mean((rr-rr.rolling(int(1/dt),center=True).mean()).dropna()**2))
    print(f"{t:18s} "+" | ".join(out)+f" | r 1s-HP {rhp:4.2f}")
