# Build approximate FS-20 Vortex Family variant airframes for the bench from the stock B extract (Family 0.7.2 source read).
# A: B wing; VTOL hardware removed (lift fan -300 kg, roll posts -25 kg each), fixed engine (-200 engine, -200 nozzle), bays +200 kg,
#    internal tank +50 kg dry; no swivel nozzle (bench: no lift system).
# C: A's removals + wing branches -450 kg; outer wing widened (Fs20Geometry.Widen on every node of the wing branches; part areas by the
#    planform stretch of their collider boxes), fins +60 % area (ExtendFin, estimated), 110 kN dry + 55 kN AB, TVC 20/550 m/s.
# NAP (NA proxy, B wing - NOT the delta): A's removals, fins and rudders removed, outer elevons split (maxSplit 45, drag per the
#    family's flat-plate rule), engine yaw vectoring 15 + pitch 20 to 550 m/s, 110 + 55 kN, T composite masses.
import json,sys,copy,numpy as np
src,var,out=sys.argv[1],sys.argv[2],sys.argv[3]
d=json.load(open(src)); T={t['id']:t for t in d['transforms']}
P={p['name']:p for p in d['parts']}
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
byname={t['name']:t['id'] for t in d['transforms']}
RootStation,NativeHalf=2.12,4.6735; TipExt=(NativeHalf-0.60)*0.25
def widen(x):
    t=min(1,max(0,(abs(x)-RootStation)/(NativeHalf-RootStation))); return x+np.sign(x)*TipExt*t*t*(3-2*t)
def addmass(n,dm): P[n]['mass']=max(1.0,P[n]['mass']+dm)
removed=[]
if var in('A','C','NAP'):
    addmass('liftFan',-300); addmass('wing2a_L',-25); addmass('wing2a_R',-25)
    addmass('engine',-200); addmass('nozzle',-200)
    addmass('weaponBay_L',100); addmass('weaponBay_R',100)
    if var=='A': addmass('liftFan',50)
if var=='C':
    addmass('weaponBay_L',10); addmass('weaponBay_R',10)
    wingRoots=['wingRoot_L','wingRoot_R','wing1a_L','wing1a_R','wing1b_L','wing1b_R']
    nodes=sorted(set(sum([branch(byname[n]) for n in wingRoots],[])))
    wparts=[p for p in d['parts'] if p['tid'] in nodes]
    W=sum(p['mass'] for p in wparts); k=(W-450)/W
    for p in wparts: p['mass']*=k
    # area ratio per part from its collider boxes' x extent in the root frame
    for p in wparts:
        R,S,Pw=world(p['tid']); x1=x2=None
        for c in p['colliders']:
            cen=Pw+R@(S*np.array(c['center'])); half=np.abs(R)@(S*np.array(c['size']))/2
            a,b=cen[0]-half[0],cen[0]+half[0]; x1=a if x1 is None else min(x1,a); x2=b if x2 is None else max(x2,b)
        if x1 is None or x2-x1<1e-3: continue
        r=(widen(x2)-widen(x1))/(x2-x1); p['wingArea']*=r; p['dragArea']*=r
        p['_ratio']=r
    # move nodes (parent first): new world = widen(old world), store local
    W0={tid:world(tid) for tid in nodes}
    newpos={tid:np.array([widen(W0[tid][2][0]),W0[tid][2][1],W0[tid][2][2]]) for tid in nodes}
    for tid in nodes:
        t=T[tid]; par=t['parent']
        PR,PS,Pp=world(par) if par in T else (np.eye(3),np.ones(3),np.zeros(3))
        pp=newpos[par] if par in newpos else Pp
        t['lp']=list((PR.T@(newpos[tid]-pp))/PS)
    for n in ['vstab_L','vstab_R']: P[n]['wingArea']*=1.6; P[n]['dragArea']*=1.6; P[n]['_ratio']=1.6
    for e in d['engines']: e['staticThrust']=110000.0; e['vec']=[20.0,0.0,0.0]; e['vecMaxSpeed']=550.0
    for n in d['nozzles']: n['ab']=[55000.0]
if var=='NAP':
    for n in ['vstab_L','vstab_R','rudder_L','rudder_R']: removed.append(P[n]['tid'])
    tm={'cockpit':.62,'nose':.62,'intake':.62,'fuselage':.53,'tail':.53,'wingRoot':.58,'wing1a':.58,'wing1b':.58,'wing2b':.58,'wingtip':.58,'wing2a':.60,'aileron':.60,'elevator':.60}
    for p in d['parts']:
        b=p['name'][:-2] if p['name'][-2:] in('_L','_R') else p['name']
        if p['name']=='SmallFighter1': p['mass']*=.62
        elif b in tm: p['mass']*=tm[b]
    for s in d['surfaces']:
        if s['name'].startswith('aileron'):
            area=P[s['name']]['wingArea']; s['maxSplit']=45.0; s['yawSplitFactor']=1.0 if s['name'].endswith('_R') else -1.0
            s['splitDrag']=max(.0035, area*np.sin(np.radians(45))*1.1/45)
    for e in d['engines']: e['staticThrust']=110000.0; e['vec']=[20.0,15.0,0.0]; e['vecMaxSpeed']=550.0
    for n in d['nozzles']: n['ab']=[55000.0]
if removed:
    rs=set(removed); d['parts']=[p for p in d['parts'] if p['tid'] not in rs]
    d['surfaces']=[s for s in d['surfaces'] if s['tid'] not in rs]
    d['tanks']=[t for t in d['tanks'] if t['tid'] not in rs]
for p in d['parts']:
    if '_ratio' in p: print(f"{var} {p['name']}: area x{p.pop('_ratio'):.3f} -> {p['wingArea']:.2f} m^2")
print(var,'mass (dry parts)',round(sum(p['mass'] for p in d['parts'])),'kg; B',round(sum(p['mass'] for p in json.load(open(src))['parts'])))
json.dump(d,open(out,'w'))
