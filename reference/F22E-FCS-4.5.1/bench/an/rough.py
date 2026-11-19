import pandas as pd, numpy as np, sys
f=sys.argv[1]; d=pd.read_csv(f); a=d[d['mode']==3].copy().reset_index(drop=True)
dt=1/60
for c in ['p','q','r']:
    a[c+'dd']=a[c].diff().diff()/dt/dt   # deg/s^3
    a[c+'dot']=a[c].diff()/dt
w=a.groupby((a.t//1.0)*1.0)
g=w.agg(alpha_min=('alpha','min'),alpha_max=('alpha','max'),ias=('ias','mean'),p=('p',lambda x: x.abs().max()),q=('q',lambda x: x.abs().max()),r=('r',lambda x: x.abs().max()),beta=('beta',lambda x: x.abs().max()),
        pdd=('pdd',lambda x: np.sqrt((x**2).mean())),qdd=('qdd',lambda x: np.sqrt((x**2).mean())),rdd=('rdd',lambda x: np.sqrt((x**2).mean())),hold=('paceHold',lambda x: (x.abs()>1).mean()),ysp=('yawSatPitch',lambda x: (x.abs()>1).mean()))
# sign changes of pdot (roll accel reversals) per second as a wobble indicator
g['pdot_rev']=w['pdot'].apply(lambda x: int(((np.sign(x).diff().abs()>1)&(x.abs().shift()>150)).sum()))
g['qdot_rev']=w['qdot'].apply(lambda x: int(((np.sign(x).diff().abs()>1)&(x.abs().shift()>100)).sum()))
g['rdot_rev']=w['rdot'].apply(lambda x: int(((np.sign(x).diff().abs()>1)&(x.abs().shift()>60)).sum()))
pd.set_option('display.width',250); pd.set_option('display.max_rows',400)
print(f, 'overall rms jerk p/q/r (deg/s^3): %.0f %.0f %.0f'%(np.sqrt((a.pdd**2).mean()),np.sqrt((a.qdd**2).mean()),np.sqrt((a.rdd**2).mean())))
print('worst 1-s windows by pitch jerk:'); print(g.sort_values('qdd',ascending=False).head(12).round(1).to_string())
print('worst by yaw jerk:'); print(g.sort_values('rdd',ascending=False).head(10).round(1).to_string())
print('worst by roll jerk:'); print(g.sort_values('pdd',ascending=False).head(10).round(1).to_string())
g.to_csv(f.split('/')[-1].replace('.csv','_rough.csv'))
