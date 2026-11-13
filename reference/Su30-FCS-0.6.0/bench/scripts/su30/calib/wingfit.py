import score,numpy as np,pandas as pd,sys
pd.set_option('display.width',250)
D=score.load(sys.argv[1] if len(sys.argv)>1 else 'grp')
D=D[(D.tas>40)&D.qm.notna()&(D.alpha>-5)&(D.alpha<80)].copy()
Iyy=395350
D['eM']=(D.qm-D.qdot)*np.pi/180*Iyy
D['Ttot']=2*D.Tn
def fit(cols,m,lbl):
    A=np.c_[D.loc[m,cols].values,np.ones(m.sum())]; y=D.eM[m].values
    c=np.linalg.lstsq(A,y,rcond=None)[0]; r=y-A@c
    print(f"{lbl:14s} n={m.sum():6d} rms {np.sqrt((y**2).mean())/1e3:5.0f}->{np.sqrt((r**2).mean())/1e3:5.0f} coef",dict(zip(cols,[float(x) for x in np.round(c[:-1],3)])),'c0 %.0f'%(c[-1]/1e3))
    return c
hi=(D.ias*3.6>200)&(D.alpha>0)
fit(['F_wing','F_flap'],hi,'all')
fit(['F_wing','F_flap','Ttot'],hi,'all+T')
fit(['F_wing','F_flap','F_canard','F_stab'],hi,'surf')
fit(['F_wing','F_flap','F_canard','F_stab','F_bodyF','F_bodyA','F_cheek'],hi,'all parts')
for lo,h in [(0,10),(10,25),(25,40),(40,55),(55,65),(65,80)]:
    m=hi&(D.alpha>lo)&(D.alpha<=h)
    fit(['F_wing','F_flap'],m,f'{lo}-{h}')
for out in sorted(D.log.unique()):
    tr=hi&(D.log!=out); te=hi&(D.log==out); cols=['F_wing','F_flap']
    c=np.linalg.lstsq(np.c_[D.loc[tr,cols].values,np.ones(tr.sum())],D.eM[tr].values,rcond=None)[0]
    r=D.eM[te].values-np.c_[D.loc[te,cols].values,np.ones(te.sum())]@c
    print('holdout',out,'coef',np.round(c,3),'test rms %.0f -> %.0f kNm'%(np.sqrt((D.eM[te]**2).mean())/1e3,np.sqrt((r**2).mean())/1e3))
print('thrust',D.Ttot.describe()[['min','mean','max']].round(0).to_dict())
