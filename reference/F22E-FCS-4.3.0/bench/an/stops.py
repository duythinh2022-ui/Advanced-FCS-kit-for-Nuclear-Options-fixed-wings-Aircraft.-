import pandas as pd, numpy as np, sys
from scipy.signal import savgol_filter
def load(f):
    d=pd.read_csv(f); d=d[d['mode'].isin([3,6])].reset_index(drop=True)
    d['rud']=(d.Rudder_L_now.abs()+d.Rudder_R_now.abs())/2/35
    d['rdot']=savgol_filter(d.r.values,9,2,deriv=1,delta=1/60)
    d['pdot']=savgol_filter(d.p.values,9,2,deriv=1,delta=1/60)
    return d
def events(d,lvl_t=None):
    rows=[]; n=len(d); i=5
    sa=np.sin(np.radians(d.alpha.abs())); ca=np.cos(np.radians(d.alpha.abs()))
    while i<n-80:
        p=d.p[i]; tgt=d.pSTarget[i]
        # onset: roll in progress >35 deg/s, target drops to <50% of |p| or flips sign
        if abs(p)>35 and (tgt*p<0 or abs(tgt)<0.5*abs(p)) and not (d.pSTarget[i-1]*d.p[i-1]<0 or abs(d.pSTarget[i-1])<0.5*abs(d.p[i-1])):
            w=d.iloc[i:i+75]  # 1.25 s
            k=int(np.argmax(w.beta.abs().values)); pk=w.beta.abs().values[k]
            rev = int((w.pSTarget*p<-5).any() and abs(w.pSTarget).max()>20)
            pS=abs(p)/max(0.2,ca[i])  # body roll -> approx velocity-vector roll
            x=pS*sa[i]*ca[i]
            # yaw decel achieved toward braking during 0.6s
            wb=d.iloc[i:i+40]
            rd=(-np.sign(p)*np.sign(d.alpha[i])*wb.rdot).max()
            rows.append(dict(t=round(d.t[i],2),lvl=(3 if lvl_t is not None and d.t[i]>lvl_t else 1),rev=rev,alpha=round(d.alpha[i],1),ias=round(d.ias[i]),p=round(p),r=round(d.r[i]),x=round(x,1),pk=round(pk,1),
                tau=round((pk-2)/x,3) if x>5 else np.nan,u=round(d.stopSlipU[i],2),rud=round(d.rud[i],2),rudmax=round(w.rud.max(),2),aB=round(d.stopSlipAB[i]),aYaw=round(d.aYaw[i]),latAcc=round(w.latAcc.iloc[2:12].max()),rdotmax=round(rd),pdotmax=round(w.pdot.abs().max()),mrg=d.stopSlipMargin[i],lim=d.stopSlipLim[i],hold=0,pred=round(w.stopSlipPred.max(),1)))
            i+=60
        i+=1
    return pd.DataFrame(rows)
if __name__=='__main__':
    pd.set_option('display.width',300); pd.set_option('display.max_rows',500)
    for f,lt in [('../logs/log0140.csv',150),('../logs/log0111.csv',None),('../logs/log0050.csv',None)]:
        d=load(f); e=events(d,lt); print(f); print(e.to_string(index=False)); e.to_csv(f.split('/')[-1].replace('.csv','_stops.csv'),index=False)
