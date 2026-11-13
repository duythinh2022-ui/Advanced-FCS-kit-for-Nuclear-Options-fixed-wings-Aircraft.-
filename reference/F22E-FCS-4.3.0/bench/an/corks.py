import pandas as pd, numpy as np
d=pd.read_csv('../logs/log0944.csv'); a=d[d['mode']==3].copy().reset_index(drop=True)
for c in ['p','q','r']: a[c+'_w']=a[c]-a[c].rolling(19,center=True,min_periods=5).mean()
a['qerr']=a.q-a.qCmd; a['perr']=a.p-a.pCmd; a['rerr']=a.r-a.rCmd
def report(name,t0,t1):
    w=a[(a.t>=t0)&(a.t<=t1)]
    print(f"{name:42s} {t0:6.1f}-{t1:6.1f}  alpha {w.alpha.iloc[0]:6.1f}->{w.alpha.iloc[-1]:6.1f} ias {w.ias.mean():4.0f} |p|max {w.p.abs().max():4.0f} |beta|max {w.beta.abs().max():4.1f} | wobble p/q/r {np.sqrt((w.p_w**2).mean()):4.1f}/{np.sqrt((w.q_w**2).mean()):4.1f}/{np.sqrt((w.r_w**2).mean()):4.1f} | q-qCmd rms {np.sqrt((w.qerr**2).mean()):4.1f} p-pCmd {np.sqrt((w.perr**2).mean()):4.1f} r-rCmd {np.sqrt((w.rerr**2).mean()):4.1f} | tgt chatter(>50 step) {int((w.pSTarget.diff().abs()>50).sum()):2d} | ysp max {w.yawSatPitch.abs().max():4.0f} hold>1 {(w.paceHold.abs()>1).mean():.2f}")
# 1. negative-AoA corkscrew starts: roll entry (|p| from <15 to >40) while alpha < -5
print("== negative-AoA corkscrew starts (roll entry while AoA < -5)")
i=0; n=len(a)
while i<n-60:
    if a.alpha[i]<-5 and abs(a.p[i])<15 and abs(a.sr[i])>0.5 and abs(a.p[i+30:i+60]).max()>40:
        report('neg-AoA roll entry',a.t[i]-0.2,a.t[i]+1.5); i+=120
    i+=1
# 2. AoA sign crossings under roll (|p|>40)
print("== AoA sign crossings under a roll (|p|>40)")
sgn=np.sign(a.alpha); cross=np.where((sgn.diff().abs()>1)&(a.p.abs()>40))[0]
last=-100
for k in cross:
    if a.t[k]-last<1.0: continue
    last=a.t[k]; dirn='neg->pos' if a.alpha[k]>0 else 'pos->neg'
    report(f'crossing {dirn}',a.t[k]-0.6,a.t[k]+0.9)
# baseline: roll entries at positive AoA 5-20 for comparison
print("== comparison: roll entries at +5..+20 AoA")
i=0; c=0
while i<n-60 and c<6:
    if 5<a.alpha[i]<20 and abs(a.p[i])<15 and abs(a.sr[i])>0.5 and abs(a.p[i+30:i+60]).max()>40:
        report('pos-AoA roll entry',a.t[i]-0.2,a.t[i]+1.5); i+=120; c+=1
    i+=1
