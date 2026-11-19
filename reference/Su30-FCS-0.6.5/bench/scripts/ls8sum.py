import pandas as pd,numpy as np,sys
tags=sys.argv[1:]; T=[78.3,124.4,129.8,613.4,858.5,950.1,1046.9,1049.8]
print('event   '+''.join(f'{t:>30}' for t in tags)); print('        '+''.join(f'{"bMax bMean aMax  vv":>30}' for t in tags))
for t in T:
    line=f'{t:7.1f} '
    for tag in tags:
        d=pd.read_csv(f'/home/claude/su30/rp/ls8/{tag}/{t}.csv'); w=d[d.t>=t-2.5]
        pS=(w.p*np.cos(np.radians(w.alpha))+w.r*np.sin(np.radians(w.alpha)))
        line+=('%5.1f %5.1f %5.1f %5.1f'%(w.beta.abs().max(),w.beta.abs().mean(),w.alpha.abs().max(),pS.abs().mean())).rjust(30)
    print(line)
