import UnityPy, json, numpy as np
env=UnityPy.load('/home/claude/work/Multirole_MTD_0.0.7.nobp')
T={};GO={};MB=[]
for o in env.objects:
    if o.type.name=='Transform':
        d=o.read_typetree(); T[o.path_id]=dict(go=d['m_GameObject']['m_PathID'],parent=d['m_Father']['m_PathID'],lp=[d['m_LocalPosition'][k] for k in 'xyz'],lr=[d['m_LocalRotation'][k] for k in 'xyzw'],ls=[d['m_LocalScale'][k] for k in 'xyz'],ch=[c['m_PathID'] for c in d['m_Children']])
    elif o.type.name=='GameObject':
        GO[o.path_id]=o.read_typetree()['m_Name']
for o in env.objects:
    if o.type.name=='MonoBehaviour':
        try:
            d=o.read_typetree(); sn=o.read().m_Script.read().m_Name
        except Exception as e: continue
        MB.append((sn,d))
go2t={t['go']:k for k,t in T.items()}
def path(tid):
    n=[]
    while tid in T: n.append(GO[T[tid]['go']]); tid=T[tid]['parent']
    return '/'.join(reversed(n))
S=json.load(open('/home/claude/work/ifrit/ifr/bench/extract/ifrit_airframe.json'))
st={t['id']:t for t in S['transforms']}
def spath(i):
    n=[]
    while i is not None: n.append(st[i]['name']); i=st[i]['parent']
    return '/'.join(reversed(n))
sp={spath(i):t for i,t in st.items()}
mp={path(k).replace('Multirole_MTD','Multirole1',1):t for k,t in T.items()}
print('stock transforms',len(sp),'mtd',len(mp))
only_m=[p for p in mp if p not in sp]; only_s=[p for p in sp if p not in mp]
print('only in MTD:',only_m); print('only in stock:',only_s[:20])
nd=0
for p,t in mp.items():
    if p in sp:
        s=sp[p]
        if np.max(np.abs(np.array(t['lp'])-np.array(s['lp'])))>1e-3 or np.max(np.abs(np.array(t['lr'])-np.array(s['lr'])))>1e-3:
            nd+=1; print('moved',p,[round(x,3) for x in s['lp']],'->',[round(x,3) for x in t['lp']], [round(x,3) for x in s['lr']],'->',[round(x,3) for x in t['lr']])
print('moved count',nd)
# AeroParts
sparts={p['name']:p for p in S['parts']}
for sn,d in MB:
    g=GO.get(d['m_GameObject']['m_PathID'])
    if sn=='AeroPart':
        s=sparts.get(g)
        vals=dict(mass=d['mass'],wingArea=d['wingArea'],dragArea=d['dragArea'],airfoil=d['airfoil'],channel=d['airflowChanneling'])
        if s is None: print('new part',g,vals); continue
        diff={k:(s.get(k),v) for k,v in vals.items() if s.get(k) is not None and abs(float(s.get(k))-float(v))>1e-6}
        if diff: print('part diff',g,diff)
ssurf={x['name']:x for x in S['surfaces']}
for sn,d in MB:
    g=GO.get(d['m_GameObject']['m_PathID'])
    if sn=='ControlSurface':
        s=ssurf.get(g); vals=dict(pitchRange=d['pitchRange'],rollRange=d['rollRange'],yawRange=d['yawRange'],servo=d['servoSpeed'],maxSplit=d['maxSplit'],splitDrag=d['splitDrag'],brakeRange=d['brakeRange'])
        if s is None: print('new surf',g,vals); continue
        diff={k:(s.get(k),v) for k,v in vals.items() if abs(float(s.get(k))-float(v))>1e-6}
        if diff: print('surf diff',g,diff)
    if sn in ('Turbojet',):
        print('engine',g,{k:d[k] for k in ('maxThrust','maxSpeed','spoolRate') if k in d}, {k:d[k] for k in d if 'Vector' in k and not isinstance(d[k],list)})
    if sn=='FuelTank': print('tank',g,d.get('fuelCapacity'))
    if sn=='Aircraft': print('aircraft', {k:v for k,v in d.items() if not isinstance(v,(dict,list))})
for sn,d in MB:
    if 'aircraftName' in d: print('params',{k:d[k] for k in ('aircraftName','DefaultFuelLevel','aircraftGLimit','maxSpeed','cornerSpeed')})
    if 'flyByWire' in d: print('fbw',{k:v for k,v in d['flyByWire'].items() if not isinstance(v,dict)})
