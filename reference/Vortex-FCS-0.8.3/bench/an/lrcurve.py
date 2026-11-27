# lrcurve.py <loadrev dir> <case> [tags...]: one loaded reversal, every 3 ticks from the stick flip
import csv,sys,numpy as np
root=sys.argv[1]; case=sys.argv[2]; tags=sys.argv[3:] or ["base","new"]
for tag in tags:
    r=list(csv.DictReader(open(f"{root}/{tag}/{case}.csv")))
    g=lambda k: np.array([float(x[k]) for x in r])
    t=g('t'); pS=g('pS'); sr=g('sr'); p=g('p'); b=g('beta'); ail=0.5*(g('ailR')-g('ailL')); a=g('alpha'); tg=g('pSTgt'); la=g('latAcc'); lr=g('loadRev'); rv=g('revA')
    try: rud=0.5*(g('rudL')+g('rudR'))
    except: rud=np.zeros(len(t))
    i0=[i for i in range(1,len(sr)) if sr[i-1]*sr[i]<0 and abs(sr[i-1])>0.5][0]; s=np.sign(sr[i0+5])
    acc=s*np.gradient(pS,t)
    z=i0
    while z<len(pS)-1 and s*pS[z]<0: z+=1
    print(f"{tag} {case}: crossing +{t[z]-t[i0]:.2f}s  every 3 ticks: t acc pS p tgt ail rud beta AoA latAcc revA")
    for k in range(i0, min(len(t), z+60), 3): print("  %5.2f %5.0f %6.1f %6.1f %6.0f %6.1f %6.1f %5.1f %5.1f %5.0f %5.0f"%(t[k]-t[i0],acc[k],pS[k],p[k],tg[k],ail[k],rud[k],b[k],a[k],la[k],rv[k]))
