import pandas as pd,numpy as np,sys
tags=sys.argv[1:]; T=[78.3,124.4,129.8,613.4,858.5,950.1,1046.9,1049.8]; O=['-0.2','-0.1','0','0.1','0.15','0.2']
def m(tag,t,o):
    d=pd.read_csv(f'/home/claude/su30/rp/ls8/{tag}_o{o}/{t}.csv'); w=d[d.t>=t-2.5]
    return w.beta.abs().max(), w.beta.abs().mean(), w.alpha.abs().max()
print('event   '+''.join(f'{t:>34}' for t in tags)); print('        '+''.join(f'{"bMax avg/worst  bMean  aMax worst":>34}' for t in tags))
R={t:[] for t in tags}
for t in T:
    line=f'{t:7.1f} '
    for tag in tags:
        v=np.array([m(tag,t,o) for o in O]); R[tag].append(v)
        line+=('%5.1f/%5.1f  %5.1f  %6.1f'%(v[:,0].mean(),v[:,0].max(),v[:,1].mean(),v[:,2].max())).rjust(34)
    print(line)
base=np.array(R[tags[0]])
for tag in tags[1:]:
    a=np.array(R[tag]); db=a[:,:,0]-base[:,:,0]; da=a[:,:,2]-base[:,:,2]
    print(f'{tag}: runs with peak sideslip >3 deg worse than off: {int((db>3).sum())}/48, >3 deg better: {int((db<-3).sum())}/48; AoA >10 deg over off: {int((da>10).sum())}; mean peak change {db.mean():+.1f}')
