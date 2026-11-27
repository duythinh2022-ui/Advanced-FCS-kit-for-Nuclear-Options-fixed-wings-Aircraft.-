import pandas as pd, numpy as np, sys
S='/tmp/claude-0/-home-claude/c3114abd-20fc-5984-be11-c25d56f03f2d/scratchpad'
D=pd.read_pickle(f'{S}/D.pkl')
GR,GS,GF,GX=1.0,0.7,1.0,0.65
rows=[]
for tag,g in D.groupby('tag'):
    g=g.reset_index(drop=True); t=g.t.values; sr=g.sr.values; sp=g.sp.values; al=g.alpha.values
    rdf=g.rdot.ewm(halflife=6).mean().values; pdf=g.pdot.ewm(halflife=6).mean().values
    def est(i,rside,side):
        e=g.iloc[i]
        if rside>0: rud,stab,flap,pr,hdr,cap=e.prZPrud,e.prZPstab,e.prZPflap,e.prZP,e.hdrZP,e.capZP
        else: rud,stab,flap,pr,hdr,cap=-e.prZNrud,-e.prZNstab,-e.prZNflap,-e.prZN,-e.hdrZN,e.capZN
        if side>0: prx,hdrx,capx=e.prXP,e.hdrXP,e.capXP
        else: prx,hdrx,capx=-e.prXN,-e.hdrXN,e.capXN
        comZ=rside*(GR*(e.rudZcmd-e.rudZnow)+GS*(e.stabZcmd-e.stabZnow)+GF*(e.flapZcmd-e.flapZnow)); comX=side*(GR*(e.rudXcmd-e.rudXnow)+GX*(e.stabXcmd-e.stabXnow)+GF*(e.flapXcmd-e.flapXnow))
        return dict(rd0=rdf[i]*rside, comZ=comZ, comX=comX, estZ=rdf[i]*rside+comZ+GR*rud+GS*stab+GF*flap, prZ=pr, hdrZ=hdr, capZ=cap, rudPr=rud, stabPr=stab, flapPr=flap,
                    pd0=pdf[i]*side, estX=pdf[i]*side+comX+GX*prx, prX=prx, hdrX=hdrx, capX=capx)
    i=1
    while i<len(g)-1:
        if sr[i-1]*sr[i]<=0 and sr[i-1]!=sr[i]:
            b=(t>=t[i]-0.3)&(t<t[i]); a=(t>t[i])&(t<=t[i]+0.3)
            if b.any() and a.any() and np.abs(sr[b]).max()>0.6 and np.abs(sr[a]).max()>0.6 and np.sign(sr[b][np.abs(sr[b]).argmax()])!=np.sign(sr[a][np.abs(sr[a]).argmax()]):
                side=np.sign(sr[a][np.abs(sr[a]).argmax()]); rside=side*np.sign(np.sin(np.radians(al[i])))
                w=(t>t[i])&(t<=t[i]+0.6)
                r=dict(tag=tag[-6:],t=round(t[i],1),kind='rev',ias=g.ias.values[i]*1.944,alpha=al[i],unmet=g.unmetZ.values[i],ySat=g.yawSat.values[i],
                       rdPk=(g.rdot.values[w]*rside).max(), pdPk=(g.pdot.values[w]*side).max())
                r.update(est(i,rside,side)); rows.append(r); i+=36; continue
        i+=1
    j=0
    while j<len(g)-90:
        if al[j]>50 and sp[j]<-0.3:
            k=j
            while k<len(g)-1 and al[k]>30 and t[k]-t[j]<2.5: k+=1
            if al[k]<=30 and t[k]-t[j]<2.5:
                w=(t>=t[j])&(t<=t[k]+0.3); rside=-np.sign(g.r.values[j]) if abs(g.r.values[j])>5 else 1
                r=dict(tag=tag[-6:],t=round(t[j],1),kind='push',ias=g.ias.values[j]*1.944,alpha=al[j],unmet=g.unmetZ.values[j],ySat=g.yawSat.values[j],
                       rdPk=(g.rdot.values[w]*rside).max(), pdPk=np.nan)
                r.update(est(j,rside,1)); rows.append(r); j=k+60; continue
        j+=1
E=pd.DataFrame(rows)
E['estZ/real']=E.estZ/E.rdPk; E['prZ/real']=(E.rd0+E.prZ)/E.rdPk; E['hdr/real']=(E.rd0+E.hdrZ)/E.rdPk; E['cap/real']=E.capZ/E.rdPk; E['estX/real']=E.estX/E.pdPk
pd.set_option('display.width',260); pd.set_option('display.max_rows',400)
cols=['tag','t','kind','ias','alpha','unmet','ySat','rdPk','rd0','comZ','rudPr','stabPr','flapPr','estZ','prZ','hdrZ','capZ','estZ/real','prZ/real','hdr/real','cap/real','pdPk','estX','capX','estX/real']
if '-v' in sys.argv: print(E[cols].round(1).to_string())
def summ(sel,name):
    s=E[sel]; q=lambda c: f'{s[c].median():.2f} [{s[c].quantile(.1):.2f}..{s[c].quantile(.9):.2f}]'
    print(f'{name}: n={len(s)} estZ/real {q("estZ/real")} | prZ/real {q("prZ/real")} | hdr/real {q("hdr/real")} | cap2.7/real {q("cap/real")} | estX/real {q("estX/real")}')
need=E.rdPk>30
summ((E.kind=='rev')&(E.alpha<40)&need&(~E.tag.isin(['095544','095730'])),'reversals <40 AoA, yaw needed >30 (2.7.1+)')
summ((E.kind=='rev')&(E.alpha>=40)&need,'reversals >=40 AoA')
summ((E.kind=='push')&need,'push-overs from 60+, yaw needed >30')
summ(((E.unmet>10)|(E.ySat>0.2))&need,'yaw-limited (unmet>10 or yawSat>0.2)')
E.to_pickle(f'{S}/E2.pkl')
