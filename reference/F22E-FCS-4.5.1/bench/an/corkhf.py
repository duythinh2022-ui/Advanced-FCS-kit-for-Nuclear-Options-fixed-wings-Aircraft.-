import pandas as pd, numpy as np
S='/tmp/claude-0/-home-claude/c3114abd-20fc-5984-be11-c25d56f03f2d/scratchpad'
from scipy.signal import butter, sosfiltfilt
HOLD=6.0; CASES=[]
for ias in [250,350,450,550]:
    for ps in [0.6,0.8,1.0]: CASES.append((f'pull{ps}+roll@0.3 {ias}',ias,ps,0.3,1.0))
for ias in [250,350,450]:
    CASES.append((f'pull1.0+roll@0 {ias}',ias,1.0,0.0,1.0)); CASES.append((f'roll-then-pull1.0 {ias}',ias,1.0,-1.0,1.0))
for ias in [350,450]: CASES.append((f'pull0.8+roll@0.3 {ias} thr0.6',ias,0.8,0.3,0.6))
def win(rd): return (rd+0.5,rd+HOLD) if rd>=0 else (1.3,HOLD+0.8)
sos=butter(3,1.5,'hp',fs=60,output='sos'); sol=butter(3,1.5,'lp',fs=60,output='sos')
rows=[]
for i,(name,ias,ps,rd,thr) in enumerate(CASES):
    r=dict(case=name)
    for tag in ['OLD','REF']:
        d=pd.read_csv(f'{S}/cork/{tag}/c{i}.csv'); w=win(rd); s=d[(d.t>=w[0]+1.0)&(d.t<=w[1])]
        for c in ['q','p','beta','alpha']:
            x=s[c].values; hf=sosfiltfilt(sos,x); lf=sosfiltfilt(sol,x)
            r[f'{tag}_{c}HF']=round(np.sqrt((hf**2).mean()),2)
            if c in ('alpha','q'): r[f'{tag}_{c}LFpp']=round(lf.max()-lf.min(),1)
    rows.append(r)
df=pd.DataFrame(rows); pd.set_option('display.width',300); pd.set_option('display.max_columns',40)
cols=['case']+[f'{t}_{c}HF' for c in ['q','p','beta'] for t in ['OLD','REF']]+['OLD_alphaLFpp','REF_alphaLFpp','OLD_qLFpp','REF_qLFpp']
print(df[cols].to_string(index=False)); print('means'); print(df[cols[1:]].mean().round(2).to_string())
df.to_csv(S+'/cork/hf.csv',index=False)
