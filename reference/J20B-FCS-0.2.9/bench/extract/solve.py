import json,numpy as np
exec(open('scripts/com.py').read().split("mp=json.load")[0])
mp=json.load(open('/tmp/res_5_Reasonix.J20.AeroControl.MassProperties069.json'))
a4=json.load(open('ext/a4.json')); a5=json.load(open('ext/a5.json'))
print('tanks4',[(t.get('part'),t.get('capacity')) for t in a4['tanks']]); print('tanks5',[(t.get('part'),t.get('capacity')) for t in a5['tanks']])
print('gearAxles',a4['gearAxles'])
print('parts with mass but no com 0.0.4:',[n for n,p in A.items() if p['mass'] and p['com'] is None])
print('parts with mass but no com 0.0.5:',[n for n,p in B.items() if p['mass'] and p['com'] is None])
def setup(a,P):
    T={t['id']:t for t in a['transforms']}
    partT={p['tid']:p['name'] for p in a['parts']}
    def partof(i):
        while i is not None:
            if i in partT: return partT[i]
            i=T[i]['parent']
        return None
    tanks=[(partof(t['tid']),t['capacity']) for t in a['tanks']]
    return tanks
t4=setup(a4,A); t5=setup(a5,B); print('tank parts 4',t4); print('tank parts 5',t5)
# world positions of gear axles via W from com.py loader: recompute
def axles(f,a):
    T={t['id']:t for t in a['transforms']}
    def W(i):
        t=T[i]
        if t['parent'] is None: return (np.zeros(3),np.eye(3),np.ones(3))
        P,R,S=W(t['parent']); return (P+R@(S*np.array(t['lp'])),R@q2m(t['lr']),S*np.array(t['ls']))
    return sorted(W(i)[0][2] for i in a['gearAxles'])
ax4=axles('a4',a4); ax5=axles('a5',a5); print('axle z 4',ax4,'5',ax5)
zMain=np.mean(ax4[:2])
def cgz(P,tanks,ze,full,override={}):
    M=0;mm=0
    for n,p in P.items():
        if not p['mass']: continue
        if n in override: z=override[n]
        elif p['com'] is not None: z=p['com'][2]
        elif n.startswith('engine'): z=ze
        else: z=p['pivot'][2]
        M+=p['mass']; mm+=p['mass']*z
    if full:
        for pn,c in tanks:
            p=P[pn]; z=override.get(pn, p['com'][2] if p['com'] is not None else (ze if pn.startswith('engine') else p['pivot'][2]))
            M+=c; mm+=c*z
    return mm/M
base_dry=mp['baselineCenters'][0][2]; base_full=mp['baselineCenters'][1][2]
for ze in np.arange(-5.0,-2.5,0.25):
    print(f'ze {ze:5.2f}: 0.0.4 dry {cgz(A,t4,ze,False):7.4f} full {cgz(A,t4,ze,True):7.4f}  ahead of mains dry {cgz(A,t4,ze,False)-zMain:.3f} full {cgz(A,t4,ze,True)-zMain:.3f}')
ov={e['name']:e['targetAircraftCenterOfMass'][2] for e in mp['parts'] if 'targetAircraftCenterOfMass' in e}
print('table entries',[(e['name'],sorted(e.keys())) for e in mp['parts'] if 'targetAircraftCenterOfMass' not in e][:5], len(mp['parts']))
print('0.0.5 with table: dry %.4f full %.4f (table baseline %.4f %.4f); ahead of mains dry %.3f full %.3f'%(cgz(B,t5,0,False,ov),cgz(B,t5,0,True,ov),base_dry,base_full,cgz(B,t5,0,False,ov)-zMain,cgz(B,t5,0,True,ov)-zMain))
for e in mp['parts']:
    if 'targetAircraftCenterOfMass' not in e: ov[e['name']]=B[e['name']]['pivot'][2]+e['localCenterOfMass'][2]
print({k:round(v,3) for k,v in ov.items()})
print('0.0.5 with table+intakes: dry %.4f full %.4f (baseline %.4f %.4f); ahead of mains dry %.3f full %.3f'%(cgz(B,t5,0,False,ov),cgz(B,t5,0,True,ov),base_dry,base_full,cgz(B,t5,0,False,ov)-zMain,cgz(B,t5,0,True,ov)-zMain))
print('0.0.5 uncorrected: dry %.4f full %.4f (mod says %.4f %.4f)'%(cgz(B,t5,0,False),cgz(B,t5,0,True),mp['uncorrectedCenters'][0][2],mp['uncorrectedCenters'][1][2]))
from collections import Counter
c=Counter(); [c.update({p:cap}) for p,cap in t5]; print('fuel by part 0.0.5',dict(c)); c=Counter(); [c.update({p:cap}) for p,cap in t4]; print('fuel by part 0.0.4',dict(c))
def inertia(P,tanks,ze,ov):
    pts=[]
    for n,p in P.items():
        if not p['mass']: continue
        if n in ov: z=ov[n]
        elif p['com'] is not None: z=p['com'][2]
        elif n.startswith('engine'): z=ze
        else: z=p['pivot'][2]
        pts.append((p['mass'],z,n))
    for pn,c in tanks:
        z=ov.get(pn, P[pn]['com'][2] if P[pn]['com'] is not None else P[pn]['pivot'][2]); pts.append((c,z,pn+'(fuel)'))
    M=sum(m for m,_,_ in pts); cg=sum(m*z for m,z,_ in pts)/M
    return sum(m*(z-cg)**2 for m,z,_ in pts), cg
I4,c4=inertia(A,t4,-3.85,{}); I5,c5=inertia(B,t5,0,ov)
print(f'pitch I about CG (z-spread only, full fuel): 0.0.4 {I4:.0f} kg m2 (cg {c4:.3f}), 0.0.5 {I5:.0f} (cg {c5:.3f}), ratio {I5/I4:.3f}')
