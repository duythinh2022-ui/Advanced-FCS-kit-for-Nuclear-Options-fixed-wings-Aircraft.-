import score,numpy as np,pandas as pd,sys
pd.set_option('display.width',250)
tag=sys.argv[1] if len(sys.argv)>1 else 'grp'
D=score.load(tag)
D=D[(D.tas>40)&D.qm.notna()&(D.alpha>-5)&(D.alpha<80)].copy()
Iyy=395350
G=['wing','flap','cheek','canard','stab','fin','bodyF','bodyA']
D['eM']=(D.qm-D.qdot)*np.pi/180*Iyy
D['Fn']=sum(D['F_'+g] for g in G)
D['ab']=pd.cut(D.alpha,[0,10,25,40,55,65,80])
print('moments by group (kN m per 1e4 Pa), + = nose up')
print((D[['M_'+g for g in G]].div(D.qbar/1e4,axis=0).assign(ab=D.ab).groupby('ab',observed=True).mean()/1000).round(0).to_string())
print('normal force (kN per 1e4 Pa)'); print((D[['F_'+g for g in G]].div(D.qbar/1e4,axis=0).assign(ab=D.ab).groupby('ab',observed=True).mean()/1000).round(0).to_string())
print('needed moment change (kN m per 1e4 Pa):',(D.eM/(D.qbar/1e4)/1000).groupby(D.ab,observed=True).mean().round(0).to_dict())
m0=(D.ias*3.6>200)
for lo,hi in [(5,25),(25,40),(40,55),(55,65),(65,80)]:
    m=(D.alpha>lo)&(D.alpha<=hi)&m0; y=D.eM[m].values
    for name,cols in [('Fn',['Fn']),('groups',['M_'+g for g in G]),('stab+can',['M_stab','M_canard']),('body+wing',['M_bodyF','M_bodyA','M_wing'])]:
        A=np.c_[D.loc[m,cols].values,np.ones(m.sum())]
        c=np.linalg.lstsq(A,y,rcond=None)[0]; r=y-A@c
        print(f"AoA {lo}-{hi} n={m.sum()} {name}: rms {np.sqrt((y**2).mean())/1e3:.0f}->{np.sqrt((r**2).mean())/1e3:.0f} kNm  coef",np.round(c[:-1],3),'c0 %.0f'%(c[-1]/1e3))
