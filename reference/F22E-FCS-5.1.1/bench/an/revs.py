# full-stick lateral reversals in an FCS log: AoA, IAS, velocity-roll before, time to zero / to 90% of the new rate, brake accel, pace hold, beta
import sys, numpy as np, pandas as pd
d=pd.read_csv(sys.argv[1]); d=d[d['mode'].isin([3,6])].reset_index(drop=True)
a=np.radians(d.alpha); d['pS']=d.p*np.cos(a)+d.r*np.sin(a)
ias_kt=d.ias*1.94384
ev=[]; sr=d.sr.values; t=d.t.values
i=1
while i<len(d)-1:
    if abs(sr[i-1])>0.8 and sr[i]*sr[i-1]<0 or (abs(sr[i-1])>0.8 and abs(sr[i])<0.8 and np.sign(sr[min(i+10,len(d)-1)])==-np.sign(sr[i-1]) and abs(sr[min(i+10,len(d)-1)])>0.8):
        s0=np.sign(sr[i-1]); t0=t[i]
        w=d[(d.t>=t0)&(d.t<=t0+4)]
        if len(w)<30 or (np.sign(w.sr.iloc[min(20,len(w)-1)])!=-s0): i+=1; continue
        pre=d[(d.t>t0-0.3)&(d.t<t0)]; p0=pre.pS.mean()
        if abs(p0)<10: i+=30; continue
        held=w[np.sign(w.sr)==-s0]
        z=w[w.pS*s0<=0]; tz=(z.t.iloc[0]-t0) if len(z) else np.nan
        w2=w[(w.t>t0+tz)] if tz==tz else w.iloc[:0]
        tgt=w2.pS.abs().max() if len(w2) else np.nan
        r90=w2[w2.pS.abs()>=0.9*tgt] if len(w2) else w2
        t90=(r90.t.iloc[0]-t0) if len(r90) else np.nan
        dur=held.t.iloc[-1]-t0 if len(held) else 0
        ev.append(dict(t=round(t0,1),aoa=round(pre.alpha.mean(),1),kt=round(ias_kt[pre.index].mean()),pS0=round(p0),tZero=round(tz,2),t90=round(t90,2),newMax=round(tgt) if tgt==tgt else np.nan,brake=round(abs(p0)/tz) if tz==tz and tz>0 else np.nan,latAccMed=round(w.latAcc.iloc[:int(max(5,tz*60))].median()) if tz==tz else np.nan,paceHoldMax=round(w.paceHold.abs().iloc[:120].max(),1),betaPk=round(w.beta.abs().iloc[:180].max(),1),stickHeld=round(dur,1),pSmax=round(w.pSmax.iloc[:60].mean())))
        i+=60
    else: i+=1
r=pd.DataFrame(ev); pd.set_option('display.width',250); print(r.to_string(index=False))
