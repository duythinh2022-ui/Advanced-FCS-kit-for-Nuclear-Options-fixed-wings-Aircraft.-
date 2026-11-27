# FS-20 Vortex Family T / NA bench airframes from the stock B extract + the family's own Fs20T geometry asset (Fs20T.bin, parsed by
# ftasset.py), following Fs20TGeometry.Apply / Fs20TDamageSections.Allocate / Fs20TMass / Fs20NAAirframe (family 0.7.2):
# lift sections' areas x(After/Before), drag x(1 + 0.35 growth), mass +0.25 growth (budget parts: that skin goes to the damage-section
# panels, the budget keeps its native area/drag/mass); lift centres Widen(native) + (AfterCenter - BeforeCenter) (not for budget parts);
# mass centres Widen(native); wing branch nodes widened; 8 damage-section panels (own parts at their region centre, the budget's airfoil
# and lift direction, area = the budget's extra area x region share); composite masses (Fs20TMass); NA: VTOL hardware and fins removed,
# outer elevons split 45 deg, engine 110 + 55 kN, TVC 20 / 15 to 550 m/s. Force points are set through each part's collider box centre
# (its COM in the bench) and centerOfLift. Not modelled: the hull colliders' own inertia shape, the T slats' section increment.
import json,sys,copy,numpy as np
src,asset,var,out=sys.argv[1],sys.argv[2],sys.argv[3],sys.argv[4]
d=json.load(open(src)); A=json.load(open(asset)); T={t['id']:t for t in d['transforms']}
byname={t['name']:t['id'] for t in d['transforms']}; P={p['name']:p for p in d['parts']}; Ptid={p['tid']:p for p in d['parts']}
def q2m(q):
    x,y,z,w=q; return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
def world(tid):
    t=T[tid]; R=q2m(t['lr']); S=np.array(t['ls']); p=np.array(t['lp'])
    if t['parent'] is None or t['parent'] not in T: return R,np.ones(3),p
    PR,PS,Pp=world(t['parent']); return PR@R, PS*S, Pp+PR@(PS*p)
kids={}
for t in d['transforms']: kids.setdefault(t['parent'],[]).append(t['id'])
def branch(tid):
    o=[tid]
    for c in kids.get(tid,[]): o+=branch(c)
    return o
RS,NH=2.12,4.6735; TE=(NH-0.60)*0.25
def widen(p):
    p=np.array(p,float); t=min(1,max(0,(abs(p[0])-RS)/(NH-RS))); p[0]+=np.sign(p[0])*TE*t*t*(3-2*t); return p
def com_of(p):
    # the bench's COM: volume-weighted collider boxes (as Airframe.BuildBodies)
    tot=0; c=np.zeros(3)
    for col in p['colliders']:
        if col.get('trigger') or 'nomesh' in col: continue
        R,S,Pw=world(col['tid'])
        if col['type']=='CapsuleCollider':
            r_,h_=col['radius'],col['height']; sz=[2*r_]*3; sz[col['dir']]=max(h_,2*r_)
        else: sz=col['size']
        v=float(np.prod(np.maximum(0.05,np.abs(np.array(sz)*S)))); c+=v*(Pw+R@(S*np.array(col['center']))); tot+=v
    if tot==0: R,S,Pw=world(p['tid']); return Pw
    return c/tot
# native mass / lift centres (root frame) before anything moves
mc={};lc={}
for p in d['parts']:
    c=com_of(p); mc[p['name']]=c
    LR,_,_=world(p['liftNormal']) if p['liftNormal'] in T else world(p['tid'])
    lc[p['name']]=c+LR@np.array(p['col'])
# widen the wing branches' nodes (RepositionWingNodes)
wingRoots=['wingRoot_L','wingRoot_R','wing1a_L','wing1a_R','wing1b_L','wing1b_R']
nodes=sorted(set(sum([branch(byname[n]) for n in wingRoots],[])))
W0={tid:world(tid) for tid in nodes}; newpos={tid:widen(W0[tid][2]) for tid in nodes}
for tid in nodes:
    t=T[tid]; par=t['parent']; PR,PS,Pp=world(par) if par in T else (np.eye(3),np.ones(3),np.zeros(3))
    pp=newpos[par] if par in newpos else Pp
    t['lp']=list((PR.T@(newpos[tid]-pp))/PS)
regions=A['regions']; budgets={r['Budget'].split('/')[-1] for r in regions}
native={n:(P[n]['wingArea'],P[n]['dragArea'],P[n]['mass']) for n in P}
target_mc={n:widen(mc[n]) if any(n==T[x]['name'] for x in nodes) else mc[n] for n in P}
target_lc={n:widen(lc[n]) if any(n==T[x]['name'] for x in nodes) else lc[n] for n in P}
added={}
for a in A['areas']:
    n=a['Path'].split('/')[-1]; p=P[n]; r=a['After']/a['Before']; g=max(0,r-1)
    p['wingArea']*=r; p['dragArea']*=1+g*0.35; add=p['mass']*g*0.25; p['mass']+=add; added[n]=add
    delta=np.array(a['AfterCenter'])-np.array(a['BeforeCenter'])
    if n not in budgets:
        target_lc[n]=target_lc[n]+delta
        if add>0: target_mc[n]=target_mc[n]+delta*(add/p['mass'])
# damage-section panels
nid=900000
for b in budgets:
    rs=[r for r in regions if r['Budget'].split('/')[-1]==b]; tot=sum(r['Area'] for r in rs)
    bp=P[b]; nA,nD,nM=native[b]; skin=bp['mass']-nM; exA=bp['wingArea']-nA; exD=bp['dragArea']-nD
    LR,_,_=world(bp['liftNormal'])
    for r in rs:
        w=r['Area']/tot; att=byname[r['Attachment'].split('/')[-1]]
        AR,AS,Ap=world(att); c=np.array(r['Center'])
        nid+=1; tid=str(nid); nid+=1; lid=str(nid)
        d['transforms'].append(dict(id=tid,name='FS20T region '+r['Id'],parent=att,lp=list((AR.T@(c-Ap))/AS),lr=[0,0,0,1],ls=[1,1,1]))
        T[tid]=d['transforms'][-1]
        # lift direction = the budget's (world rotation), as a child of the region frame
        q=None
        import math
        m=LR; tr=m[0,0]+m[1,1]+m[2,2]
        if tr>0:
            s=math.sqrt(tr+1)*2; q=[(m[2,1]-m[1,2])/s,(m[0,2]-m[2,0])/s,(m[1,0]-m[0,1])/s,0.25*s]
        else:
            q=[0,0,0,1]
        RR,_,_=world(tid); qloc=q  # region frame rotation = root (identity) in the asset, so the world rotation is the local one
        d['transforms'].append(dict(id=lid,name='Section lift direction',parent=tid,lp=[0,0,0],lr=qloc,ls=[1,1,1])); T[lid]=d['transforms'][-1]
        d['parts'].append(dict(name='T_'+r['Id'],tid=tid,mass=skin*w,wingArea=exA*w,dragArea=exD*w,airfoil=bp['airfoil'],col=[0,0,0],liftNormal=lid,channel=bp['channel'],anchor=None,streamlining=bp['streamlining'],comT=None,colliders=[dict(type='BoxCollider',tid=tid,trigger=False,enabled=True,center=[0,0,0],size=[0.6,0.08,0.6])]))
        P['T_'+r['Id']]=d['parts'][-1]; target_mc['T_'+r['Id']]=c; target_lc['T_'+r['Id']]=c
    bp['wingArea'],bp['dragArea'],bp['mass']=nA,nD,nM
# composite masses (Fs20TMass)
tm={'cockpit':.62,'nose':.62,'intake':.62,'fuselage':.53,'tail':.53,'wingRoot':.58,'wing1a':.58,'wing1b':.58,'wing2b':.58,'wingtip':.58,'wing2a':.60,'aileron':.60,'elevator':.60,'vstab':.60,'rudder':.60}
for p in d['parts']:
    n=p['name']; b=n[:-2] if n[-2:] in('_L','_R') else n
    if n.startswith('T_'): p['mass']*=.46
    elif n=='SmallFighter1': p['mass']*=.62
    elif b in tm: p['mass']*=tm[b]
removed=set()
if var=='NA':
    P['liftFan']['mass']-=300; P['wing2a_L']['mass']-=25; P['wing2a_R']['mass']-=25
    for n in ['vstab_L','vstab_R','rudder_L','rudder_R']: removed.add(P[n]['tid'])
    P['weaponBay_L']['mass']+=95; P['weaponBay_R']['mass']+=95   # bays + hook well (Fs20BaySpec)
    for s in d['surfaces']:
        if s['name'].startswith('aileron'):
            area=P[s['name']]['wingArea']; s['maxSplit']=45.0; s['yawSplitFactor']=1.0 if s['name'].endswith('_R') else -1.0
            s['splitDrag']=max(.0035, area*np.sin(np.radians(45))*1.1/45)
    for e in d['engines']: e['staticThrust']=110000.0; e['vec']=[20.0,15.0,0.0]; e['vecMaxSpeed']=550.0
    for n in d['nozzles']: n['ab']=[55000.0]
# place COM and force point: a single box collider centred on the target COM, centerOfLift to the target force point
for p in d['parts']:
    n=p['name']
    if p['tid'] in removed or n not in target_mc: continue
    if np.allclose(target_mc[n],mc.get(n,target_mc[n])) and np.allclose(target_lc[n],lc.get(n,target_lc[n])) and not n.startswith('T_') and n not in [T[x]['name'] for x in nodes]: continue
    R,S,Pw=world(p['tid']); cm=np.array(target_mc[n])
    size=next((c['size'] for c in p['colliders'] if 'size' in c),[0.5,0.5,0.5])
    p['colliders']=[dict(type='BoxCollider',tid=p['tid'],trigger=False,enabled=True,center=list((R.T@(cm-Pw))/S),size=size)]
    LR,_,_=world(p['liftNormal'])
    p['col']=list(LR.T@(np.array(target_lc[n])-cm))
if removed:
    d['parts']=[p for p in d['parts'] if p['tid'] not in removed]
    d['surfaces']=[s for s in d['surfaces'] if s['tid'] not in removed]
    d['tanks']=[t for t in d['tanks'] if t['tid'] not in removed]
dry=sum(p['mass'] for p in d['parts']); fuel=sum(t['capacity'] for t in d['tanks'])
print(var,'dry parts',round(dry),'kg, tanks',round(fuel),'kg, total area',round(sum(p['wingArea'] for p in d['parts']),1),'m^2 (B',round(sum(x['wingArea'] for x in json.load(open(src))['parts']),1),')')
json.dump(d,open(out,'w'))
