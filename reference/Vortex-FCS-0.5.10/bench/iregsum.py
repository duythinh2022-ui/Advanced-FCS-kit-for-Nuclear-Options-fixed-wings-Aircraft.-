import sys,glob,os,pandas as pd,numpy as np
tags=sys.argv[1:]
rows={}
for tag in tags:
    for f in sorted(glob.glob(f'runs/reg_{tag}/*.csv')):
        d=pd.read_csv(f); n=os.path.basename(f)[:-4]
        if len(d)<10: rows.setdefault(n,{})[tag]='FAIL'; continue
        late=d[d.t>d.t.max()-2.5]
        # VV roll mean while roll stick in
        roll=d[d.sr.abs()>0.5] if 'sr' in d else d
        pv=roll.pV.abs().mean() if len(roll) else float('nan')
        rows.setdefault(n,{})[tag]=f"b{d.beta.abs().max():4.1f} late{late.beta.std():4.2f} pV{pv:4.1f} a{d.alpha.max():3.0f} dep{int((d.beta.abs()>15).any())}"
for n in sorted(rows):
    print(f"{n:14s} "+" | ".join(f"{t}: {rows[n].get(t,'-')}" for t in tags))
