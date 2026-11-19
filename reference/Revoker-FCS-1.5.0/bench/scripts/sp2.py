import numpy as np
from scipy.signal import find_peaks
def sim(K,T,tm,lag=0.0,g=1.0,dt=0.005,Tend=14,dist=0.0,b0=1.0,sat=None):
    # inner loop: pure delay T then first-order lag 'lag'; predictor: beta - tm*(cmd - meas)
    n=int(Tend/dt); D=int(T/dt); b=b0; rs=np.zeros(n); bh=np.zeros(n); bh[0]=b; rsf=0; meas=0
    for i in range(1,n):
        rd = rs[i-1-D] if i-1-D>=0 else 0
        meas = meas + (g*rd-meas)*dt/(lag+dt) if lag>0 else g*rd
        b += (-meas+dist)*dt
        u=K*(b - tm*(rsf-meas))
        if sat: u=np.clip(u,-sat,sat)
        rsf += (u-rsf)*dt/(0.12+dt); rs[i]=rsf; bh[i]=b
    t=np.arange(n)*dt
    pk,_=find_peaks(np.abs(bh-bh[-1])); v=np.abs(bh[pk]-bh[-1]); v=v[v>2e-3*b0+1e-4]
    ratio=(v[2]/v[0]) if len(v)>2 else 0
    idx=np.where(np.abs(bh-bh[-1])>0.05*max(b0,1e-9))[0]; ts=t[idx[-1]] if len(idx) else 0
    return ratio, ts, bh[-1]
for tm in [0,0.2,0.3,0.4]:
    print('tm',tm)
    for T,lag in [(0.05,0.05),(0.1,0.0),(0.2,0.0),(0.3,0.0),(0.35,0.0),(0.2,0.15),(0.45,0.0)]:
        r=[sim(4,T,tm,lag,g) for g in [0.8,1.0,1.2]]
        print('  delay %.2f lag %.2f: worst cyc %.2f worst ts %.1f'%(T,lag,max(x[0] for x in r),max(x[1] for x in r)), ' sat2 cyc %.2f'%sim(4,T,tm,lag,sat=2)[0], ' dist beta %.2f'%sim(4,T,tm,lag,b0=0,dist=1)[2])
