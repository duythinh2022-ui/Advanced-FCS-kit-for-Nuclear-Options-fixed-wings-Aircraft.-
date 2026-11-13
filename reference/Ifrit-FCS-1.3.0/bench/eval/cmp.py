import csv,glob,os,collections,sys,math
D=sys.argv[1]; T=sys.argv[2].split(',')
def ps(x): a=math.radians(float(x['alpha'])); return float(x['p'])*math.cos(a)+float(x['r'])*math.sin(a)
def st(f):
    r=list(csv.DictReader(open(f))); n=os.path.basename(f)
    if '_leaf_' in n:
        w=[x for x in r if abs(float(x['yawIn']))>0.5]
        rr=[abs(float(x['r'])) for x in w]
        return 'r mean %4.1f pk %4.1f |b| %4.1f'%(sum(rr)/len(rr),max(rr),max(abs(float(x['beta'])) for x in w))
    i0=next(i for i,x in enumerate(r) if abs(float(x['sr']))>0.5)
    b=max(abs(float(x['beta'])) for x in r[i0:])
    sr=[float(x['sr']) for x in r]; t=[float(x['t']) for x in r]
    ir=next((i for i in range(i0,len(r)) if sr[i]>0.5 and min(sr[i0:i] or [0])<-0.5),None)
    extra=''
    if ir:
        iz=next((i for i in range(ir,len(r)) if ps(r[i])>0),None)
        extra=' rev %.2fs'%(t[iz]-t[ir]) if iz else ' rev -'
    else:
        phi=[float(x['phi']) for x in r]; tot=0
        for i in range(i0+1,len(r)):
            tot+=((phi[i]-phi[i-1]+180)%360-180)
            if t[i]-t[i0]>=2.0: break
        extra=' bank2s %4.0f'%tot
    return '|b| %4.1f%s'%(b,extra)
d=collections.defaultdict(dict)
for f in glob.glob(D+'/*.csv'):
    n=os.path.basename(f)[:-4]; tag,k=n.split('_',1); d[k][tag]=st(f)
for k in sorted(d): print('%-16s'%k,' | '.join('%s %s'%(t,d[k].get(t,'-')) for t in T))
