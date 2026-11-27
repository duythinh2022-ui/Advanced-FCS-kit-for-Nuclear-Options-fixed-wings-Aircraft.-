import sys,pandas as pd
tags=sys.argv[1:]; T={}
for t in tags:
    L=[l for l in open(f'/tmp/aoa_{t}.txt') if l.strip() and 'FAILED' not in l and not l.startswith('/')]
    hdr=L[0].split(); rows=[l.split() for l in L[1:]]
    T[t]=pd.DataFrame(rows,columns=hdr).set_index('case').apply(pd.to_numeric,errors='coerce')
def show(case,cols):
    print(f'{case:12s} '+' | '.join(f'{t}: '+' '.join(f'{c}={T[t].loc[case,c]:.1f}' for c in cols) for t in tags if case in T[t].index))
for v in (200,300,450):
    show(f'hold_{v}',['aMax','aHold','tTo90%'])
    show(f'lr_{v}',['aRoll','aRollMin','pSmean','bPk'])
    show(f'rev_{v}',['aRoll','pSmean','tRev','bPk'])
    show(f'ped_{v}',['aRoll','rMean','bPk'])
    show(f'rel_{v}',['aMax','t<30','t<15','aUnder'])
for c in ['idle_lr_300','nt_lr_300']: show(c,['aMax','aRoll','pSmean','bPk'])
print('dep flags:', {t:int(T[t].dep.sum()) for t in tags})
