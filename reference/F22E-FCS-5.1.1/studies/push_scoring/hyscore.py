import sys,numpy as np,pandas as pd,glob,os
tags=sys.argv[1:]
def m(f,t):
    w=f[f.t>=t].reset_index(drop=True); a=w.alpha_b.values
    msk=(np.abs(a)>12)&(np.abs(a)<58); s=-np.sign(a[msk]); e=s*(w.q_b.values[msk]-w.q_cmd.values[msk])
    ap=np.radians(a); pV=np.abs(w.p_b*np.cos(ap)+w.r_b*np.sin(ap)).values; sm=np.convolve(pV,np.ones(7)/7,'same')
    ist=np.argmax(sm<3) if (sm<3).any() else len(sm)-1
    ia=np.argmax(np.abs(a)<10) if (np.abs(a)<10).any() else len(a)-1
    return e.max(), w.beta_b.abs().max(), w.t[ist]-t, w.t[ia]-t
R={}
for tag in tags:
    rows=[]
    for fn in sorted(glob.glob(f'score/hy_{tag}/*.csv')):
        k=os.path.basename(fn)[:-4]; t=float(k.split('_')[-1]); f=pd.read_csv(fn); rows.append((k,)+m(f,t))
    R[tag]=pd.DataFrame(rows,columns=['k','ov','beta','rollstop','t10']).set_index('k')
for tag in tags:
    x=R[tag]; print(f'{tag:10s} n={len(x)} overshoot {x.ov.median():5.1f}/{x.ov.quantile(.9):5.1f}/{x.ov.max():5.1f}  sideslip {x.beta.median():4.1f}/{x.beta.quantile(.9):4.1f}/{x.beta.max():4.1f}  roll<3 {x.rollstop.median():.2f}/{x.rollstop.quantile(.9):.2f}  t10 {x.t10.median():.2f}/{x.t10.quantile(.9):.2f}')
b=R[tags[0]]
for tag in tags[1:]:
    x=R[tag]; print(f'  {tag} vs {tags[0]}: overshoot better {(x.ov<b.ov-2).sum()} worse {(x.ov>b.ov+2).sum()} | sideslip better {(x.beta<b.beta-1).sum()} worse {(x.beta>b.beta+1).sum()}')
