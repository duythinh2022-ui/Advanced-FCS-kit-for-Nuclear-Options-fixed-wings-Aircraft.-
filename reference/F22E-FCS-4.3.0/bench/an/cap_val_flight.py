# In-flight validation of the 2.7.5 estimator columns: per reversal / push-over, logged ceCapZ vs the realised peak yaw
# acceleration; plus a horizon-discounted committed term recomputed from the surfaces' cmd/now travel.
import pandas as pd, numpy as np, sys
from scipy.signal import savgol_filter
pd.set_option('display.width',300); pd.set_option('display.max_rows',400)
f=sys.argv[1]; TH=float(sys.argv[2]) if len(sys.argv)>2 else 0.15; RATE=100.
d=pd.read_csv(f); d=d[d['mode'].isin([3,6])&(d.tas>40)].reset_index(drop=True)
for c in 'pqr': d[c+'dot']=savgol_filter(d[c].values,9,3,deriv=1,delta=1/60)
G={'r':1.0,'s':0.7,'f':1.0}
def disc(group):
    names={'r':['Rudder_L','Rudder_R'],'s':['Elevator_L','Elevator_R'],'f':['Aileron_L','Aileron_R','Flap_L','Flap_R']}[group]
    num=np.zeros(len(d)); den=np.zeros(len(d))
    for n in names:
        tr=(d[n+'_cmd']-d[n+'_now']).abs().values
        num+=np.minimum(tr,RATE*TH); den+=tr
    return np.where(den>1e-3,num/np.maximum(den,1e-3),1.0)
dr,ds,df=disc('r'),disc('s'),disc('f')
d['comZ_disc']=d.ceCmZr*dr*G['r']+d.ceCmZs*ds*G['s']+d.ceCmZf*df*G['f']
d['comZ_fix']=d.ceCmZr*G['r']+d.ceCmZs*G['s']+d.ceCmZf*G['f']
d['capZP_fix']=d.ceWdZ+d.comZ_fix+G['r']*d.cePrZPr+G['s']*d.cePrZPs+G['f']*d.cePrZPf
d['capZN_fix']=-d.ceWdZ-d.comZ_fix+G['r']*d.cePrZNr+G['s']*d.cePrZNs+G['f']*d.cePrZNf
d['capZP_disc']=d.ceWdZ+d.comZ_disc+G['r']*d.cePrZPr+G['s']*d.cePrZPs+G['f']*d.cePrZPf
d['capZN_disc']=-d.ceWdZ-d.comZ_disc+G['r']*d.cePrZNr+G['s']*d.cePrZNs+G['f']*d.cePrZNf
t=d.t.values; sr=d.sr.values; sp=d.sp.values; al=d.alpha.values
rows=[]
def rec(i,rside,kind,w):
    e=d.iloc[i]
    pick=lambda P,N: e[P] if rside>0 else e[N]
    rows.append(dict(t=round(t[i],1),kind=kind,ias=e.ias*1.944,alpha=al[i],r0=e.r,unmet=e.unmetZ,ySat=e.yawSat,
        real=(d.rdot.values[w]*rside).max(), wd=e.ceWdZ*rside, ceCap=pick('ceCapZP','ceCapZN'), ceRaw=pick('ceCapZPraw','ceCapZNraw'),
        fix=pick('capZP_fix','capZN_fix'), disc=pick('capZP_disc','capZN_disc'), old=pick('capZP','capZN')))
i=1
while i<len(d)-1:
    if sr[i-1]*sr[i]<=0 and sr[i-1]!=sr[i]:
        b=(t>=t[i]-0.3)&(t<t[i]); a=(t>t[i])&(t<=t[i]+0.3)
        if b.any() and a.any() and np.abs(sr[b]).max()>0.6 and np.abs(sr[a]).max()>0.6 and np.sign(sr[b][np.abs(sr[b]).argmax()])!=np.sign(sr[a][np.abs(sr[a]).argmax()]):
            side=np.sign(sr[a][np.abs(sr[a]).argmax()]); rside=side*np.sign(np.sin(np.radians(al[i])))
            rec(i,rside,'rev',(t>t[i])&(t<=t[i]+0.6)); i+=36; continue
    i+=1
j=0
while j<len(d)-90:
    if al[j]>50 and sp[j]<-0.3:
        k=j
        while k<len(d)-1 and al[k]>30 and t[k]-t[j]<2.5: k+=1
        if al[k]<=30 and t[k]-t[j]<2.5:
            rside=-np.sign(d.r.values[j]) if abs(d.r.values[j])>5 else 1
            rec(j,rside,'push',(t>=t[j])&(t<=t[k]+0.3)); j=k+60; continue
    j+=1
E=pd.DataFrame(rows)
for c in ['ceCap','ceRaw','fix','disc','old']: E[c+'/real']=E[c]/E.real
print(E.round(1).to_string(index=False))
need=E.real>30
def q(s,c): return f'{s[c].median():.2f} [{s[c].quantile(.1):.2f}..{s[c].quantile(.9):.2f}]'
for name,sel in [('reversals <40 AoA',(E.kind=='rev')&(E.alpha<40)&need),('reversals >=40',(E.kind=='rev')&(E.alpha>=40)&need),('push-overs',(E.kind=='push')&need),('yaw-limited',((E.unmet>10)|(E.ySat>0.2))&need)]:
    s=E[sel]
    if len(s): print(f'{name}: n={len(s)} logged ceCap/real {q(s,"ceCap/real")} | gains-1 {q(s,"ceRaw/real")} | fixed gains {q(s,"fix/real")} | fixed+discounted(T={TH}) {q(s,"disc/real")} | 2.7.x capZ {q(s,"old/real")}')
