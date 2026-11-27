import sys,pandas as pd,numpy as np
tags=sys.argv[1:]; T={}
for t in tags:
    L=[l for l in open(f'/tmp/lt_{t}.txt') if l.strip() and 'FAILED' not in l and not l.startswith('/')]
    hdr=L[0].split(); rows=[l.split() for l in L[1:]]
    T[t]=pd.DataFrame(rows,columns=hdr).set_index('case').apply(pd.to_numeric,errors='coerce')
cases=T[tags[0]].index
print('case'.ljust(16)+''.join(f'{t:>26s}' for t in tags)+'   (bPk / aMax / pS or r)')
for c in cases:
    line=c.ljust(16)
    for t in tags:
        r=T[t].loc[c]; m=r.pSmean if not np.isnan(r.pSmean) else r.rMean
        line+=f'{r.bPk:9.1f} {r.aMax:6.1f} {m:6.1f}   '
    print(line)
for t in tags: print(t,'mean bPk %.2f  worst %.1f  n>10: %d'%(T[t].bPk.mean(),T[t].bPk.max(),(T[t].bPk>10).sum()))
