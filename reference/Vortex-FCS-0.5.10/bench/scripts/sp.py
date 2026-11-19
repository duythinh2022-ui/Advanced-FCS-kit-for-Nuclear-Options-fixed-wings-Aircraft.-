import numpy as np
from scipy.signal import find_peaks
def sim(K,tau,T,Tm,g=1.0,dt=0.005,Tend=12,sat=None,dist=0.0):
    n=int(Tend/dt); D=int(T/dt); Dm=int(Tm/dt); b=1.0; rs=np.zeros(n); bh=np.zeros(n); bh[0]=b; bd=0; rsf=0; bprev=b
    for i in range(1,n):
        rd = rs[i-1-D] if i-1-D>=0 else 0
        b += (-g*rd+dist)*dt
        bdr=(b-bprev)/dt; bprev=b; bd += (bdr-bd)*dt/(0.05+dt)
        pred = rs[max(0,i-Dm):i].sum()*dt if Tm>0 else 0
        bf=b-pred
        u=K*(bf+tau*bd)
        if sat: u=np.clip(u,-sat,sat)
        rsf += (u-rsf)*dt/(0.12+dt); rs[i]=rsf; bh[i]=b
    t=np.arange(n)*dt
    pk,_=find_peaks(np.abs(bh)); v=np.abs(bh[pk]); v=v[v>1e-3]
    ratio=(v[2]/v[0]) if len(v)>2 else 0
    idx=np.where(np.abs(bh-bh[-1])>0.05)[0]; ts=t[idx[-1]] if len(idx) else 0
    return ratio, ts, bh[-1]
print('Smith predictor, model T 0.3, K 4')
for T in [0.2,0.3,0.4,0.45]:
  for g in [0.8,1.0,1.2]:
    print('true T %.2f g %.1f'%(T,g),'  '.join('lead %.2f: cyc %.2f ts %.1f'%((tau,)+sim(4,tau,T,0.3,g)[:2]) for tau in [0,0.15]), '| no pred lead .15: cyc %.2f ts %.1f'%sim(4,0.15,T,0,g)[:2])
print('saturated rS (|u|<=12, beta0 = 1 -> scale: use beta0 1, sat 2)')
for T in [0.3,0.4]:
    print(T, 'pred: cyc %.2f ts %.1f'%sim(4,0,T,0.3,sat=2)[:2], 'nopred: cyc %.2f ts %.1f'%sim(4,0.15,T,0,sat=2)[:2])
print('steady disturbance (gravity/asym model error 1 deg/s): final beta')
print('pred', sim(4,0,0.3,0.3,dist=1.0)[2], 'nopred', sim(4,0.15,0.3,0,dist=1.0)[2])
def sim2(K,T,Tm,tw,g=1.0,dt=0.005,Tend=14,dist=0.0,b0=1.0,sat=None):
    n=int(Tend/dt); D=int(T/dt); Dm=int(Tm/dt); b=b0; rs=np.zeros(n); hp=np.zeros(n); bh=np.zeros(n); bh[0]=b; rsf=0; lp=0
    for i in range(1,n):
        rd = rs[i-1-D] if i-1-D>=0 else 0
        b += (-g*rd+dist)*dt
        pred = hp[max(0,i-Dm):i].sum()*dt
        u=K*(b-pred)
        if sat: u=np.clip(u,-sat,sat)
        rsf += (u-rsf)*dt/(0.12+dt); rs[i]=rsf
        lp += (rsf-lp)*dt/(tw+dt) if tw>0 else 0
        hp[i]=rsf-lp if tw>0 else rsf
        bh[i]=b
    t=np.arange(n)*dt
    pk,_=find_peaks(np.abs(bh-bh[-1])); v=np.abs(bh[pk]-bh[-1]); v=v[v>1e-3]
    ratio=(v[2]/v[0]) if len(v)>2 else 0
    idx=np.where(np.abs(bh-bh[-1])>0.05*b0)[0]; ts=t[idx[-1]] if len(idx) else 0
    return ratio, ts, bh[-1]
print('--- washout on predictor input')
for tw in [0,1.0,1.5,2.5]:
    r=[sim2(4,T,0.3,tw,g) for T in [0.2,0.3,0.4,0.45] for g in [0.8,1.0,1.2]]
    print('tw',tw,'worst cyc %.2f worst ts %.1f'%(max(x[0] for x in r),max(x[1] for x in r)),'| dist final beta %.2f'%sim2(4,0.3,0.3,tw,b0=0,dist=1.0)[2], '| sat worst cyc %.2f'%max(sim2(4,T,0.3,tw,sat=2)[0] for T in [0.3,0.4]))
print('reference no predictor K4 lead0 dist beta', sim2(4,0.3,0.0,0,b0=0,dist=1.0)[2])
print('--- short true delay, predictor 0.3, washout 1.5')
for T in [0.05,0.1,0.15]:
    print(T, ['cyc %.2f ts %.1f'%sim2(4,T,0.3,1.5,g)[:2] for g in [0.8,1.0,1.2]], '| K1.3:', 'cyc %.2f ts %.1f'%sim2(1.3,T,0.3,1.5)[:2])
