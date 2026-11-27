import json,sys,numpy as np
def q2m(q):
    x,y,z,w=q
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
def load(f):
    a=json.load(open(f)); T={t['id']:t for t in a['transforms']}
    cache={}
    def W(i):
        if i in cache: return cache[i]
        t=T[i]
        if t['parent'] is None: r=(np.zeros(3),np.eye(3),np.ones(3))
        else:
            P,R,S=W(t['parent']); r=(P+R@(S*np.array(t['lp'])),R@q2m(t['lr']),S*np.array(t['ls']))
        cache[i]=r; return r
    out={}
    for p in a['parts']:
        vs=0; m=np.zeros(3); info=[]
        for c in p['colliders']:
            if c.get('trigger') or not c.get('enabled',1): continue
            P,R,S=W(c['tid'])
            if 'hull' in c: v=c['hull']['V']; cl=np.array(c['hull']['C'])
            elif c['type']=='BoxCollider': v=np.prod(np.array(c['size'])*S); cl=S*np.array(c['center'])
            else: continue
            m+=v*(P+R@cl); vs+=v; info.append(c['type'])
        Pp,Rp,_=W(p['tid'])
        out[p['name']]=dict(com=m/vs if vs else None, pivot=Pp, mass=p['mass'], cols=info)
    return out
A=load('ext/a4.json'); B=load('ext/a5.json')
mp=json.load(open('/tmp/res_5_Reasonix.J20.AeroControl.MassProperties069.json'))
print('part        0.0.4 auto CoM z/y      0.0.5 auto CoM z/y     table prev z/y     table target z/y')
for e in mp['parts']:
    n=e['name']; a=A[n]['com']; b=B[n]['com']; pr=e['previousAircraftCenterOfMass']; t=e['targetAircraftCenterOfMass']
    f=lambda v:('   None   ' if v is None else f'{v[2]:7.3f} {v[1]:6.3f}')
    print(f"{n:<17} {f(a)}   {f(b)}   {pr[2]:7.3f} {pr[1]:6.3f}   {t[2]:7.3f} {t[1]:6.3f}  pivot4 {A[n]['pivot'][2]:.3f}"); continue
    print(f"{n:<17} {a[2]:7.3f} {a[1]:6.3f}   {b[2]:7.3f} {b[1]:6.3f}   {pr[2]:7.3f} {pr[1]:6.3f}   {t[2]:7.3f} {t[1]:6.3f}")
# aircraft dry CG (structure only, part CoMs) both versions, and with 0.0.5 targets
def cg(P,override=None):
    M=0;mm=np.zeros(3)
    for n,p in P.items():
        if not p['mass'] or p['com'] is None: continue
        c=np.array(override[n]) if override and n in override else p['com']
        M+=p['mass']; mm+=p['mass']*c
    return mm/M, M
ov={e['name']:e['targetAircraftCenterOfMass'] for e in mp['parts']}
print('dry CG 0.0.4 auto', cg(A)); print('dry CG 0.0.5 auto (uncorrected)', cg(B)); print('dry CG 0.0.5 with table', cg(B,ov))
print('table baseline/uncorrected/predicted (dry, full):', mp['baselineCenters'], mp['uncorrectedCenters'], mp['predictedCenters'])
# pitch inertia about CG (point masses at part CoMs)
def Iyy(P,override=None):
    c,M=cg(P,override); s=0
    for n,p in P.items():
        if not p['mass'] or p['com'] is None: continue
        q=np.array(override[n]) if override and n in override else p['com']
        d=q-c; s+=p['mass']*(d[1]**2+d[2]**2)
    return s
print('pitch I (point masses) 0.0.4 %.0f  0.0.5 table %.0f'%(Iyy(A),Iyy(B,ov)))
for n in ['J20NativeMinimum','cockpit','fuselage_R','engine_L','engine_R','intake_L']: print(n,'mass',A[n]['mass'],B[n]['mass'],'cols4',A[n]['cols'],'cols5',B[n]['cols'])
