import pandas as pd, numpy as np, sys, warnings; warnings.filterwarnings("ignore")
K=[(0,6.12),(4,6.12),(8,6.07),(12,5.97),(16,5.82),(20,5.66),(24,5.45),(28,5.18),(32,4.9),(36,4.6),(40,4.26),(44,3.9),(48,3.6),(65,3.0)]
def kA(a): return np.interp(abs(a),[k[0] for k in K],[k[1] for k in K])
def events(f):
    d=pd.read_csv(f); d=d[d["mode"]==3].reset_index(drop=True)
    t=d.t.values; sr=d.sr.values; p=d.p.values; r=d.r.values; a=d.alpha.values; b=d.beta.values
    out=[]; last=-9
    for i in range(1,len(d)):
        prev=np.where((t>=t[i]-0.35)&(t<t[i]))[0]
        if len(prev)==0: continue
        smax=sr[prev][np.argmax(abs(sr[prev]))]
        if abs(smax)>0.25 and (abs(sr[i])<0.08 or sr[i]*smax<-0.08) and t[i]-last>0.8:
            pre=np.where((t>=t[i]-0.6)&(t<=t[i]+0.15))[0]
            post=np.where((t>=t[i])&(t<=t[i]+1.5))[0]
            pk=pre[np.argmax(abs(p[pre]))]; rk=pre[np.argmax(abs(r[pre]))]
            bk=post[np.argmax(abs(b[post]))]
            rev=bool(np.any(sr[post][:40]*smax<-0.25))
            rs=np.sign(r[rk]) if abs(r[rk])>1 else 0
            win=np.where((t>=t[i]-0.3)&(t<=t[i]))[0]
            uL=(d.Rudder_L_now.values[win]*rs/35).max(); uR=(-d.Rudder_R_now.values[win]*rs/35).max()
            q=0.5*d.rho[i]*d.tas[i]**2/1000
            al=a[i]
            out.append(dict(f=f[-10:-4],t=t[i],kt=d.ias[i]*1.944,a=al,p=abs(p[pk]),r=abs(r[rk]),u=max(uL,uR),aB=q*kA(al),b0=abs(b[i]),bmax=abs(b[bk]),rev=int(rev),sp=d.sp[i]))
            last=t[i]
    return pd.DataFrame(out)
fs=sys.argv[1:]
e=pd.concat([events(f) for f in fs])
e=e[(e.p>15)&(e.a>5)&(e.a<40)]
e["tY"]=e.r/e.aB
e["mdl"]=2.1*np.sin(np.radians(e.a))*e.r**2/e.aB
pd.set_option("display.width",250); pd.set_option("display.max_rows",300)
print(e.sort_values("bmax").to_string(index=False,float_format=lambda x:f"{x:.2f}"))
e.to_csv("/tmp/claude-0/-home-claude/91b81c1e-955b-555c-b741-a538fa553687/scratchpad/an/game_events.csv",index=False)
