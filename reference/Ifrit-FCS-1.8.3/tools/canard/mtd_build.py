# usage: python3 mtd_build.py Multirole_MTD_0.0.7.nobp bench/extract/ifrit_airframe.json bench/extract/mtd_airframe.json
import UnityPy, json, copy
env=UnityPy.load(__import__('sys').argv[1])
T={};GO={}
for o in env.objects:
    if o.type.name=='Transform':
        d=o.read_typetree(); T[o.path_id]=dict(go=d['m_GameObject']['m_PathID'],parent=d['m_Father']['m_PathID'],lp=[d['m_LocalPosition'][k] for k in 'xyz'],lr=[d['m_LocalRotation'][k] for k in 'xyzw'],ls=[d['m_LocalScale'][k] for k in 'xyz'],ch=[c['m_PathID'] for c in d['m_Children']])
    elif o.type.name=='GameObject': GO[o.path_id]=o.read_typetree()['m_Name']
def path(tid):
    n=[]
    while tid in T: n.append(GO[T[tid]['go']]); tid=T[tid]['parent']
    return '/'.join(reversed(n)).replace('Multirole_MTD','Multirole1',1)
S=json.load(open(__import__('sys').argv[2]))
O=copy.deepcopy(S)
st={t['id']:t for t in O['transforms']}
def spath(i):
    n=[]
    while i is not None: n.append(st[i]['name']); i=st[i]['parent']
    return '/'.join(reversed(n))
p2id={spath(i):i for i in st}
# 1) moved transforms (not the root)
m2id={}
for k,t in T.items():
    p=path(k)
    if p in p2id:
        m2id[k]=p2id[p]
        if T[k]['parent'] in T:  # skip root pose
            st[p2id[p]]['lp']=t['lp']; st[p2id[p]]['lr']=t['lr']
# 2) new transforms
nid=900000
for k,t in T.items():
    if k in m2id: continue
    nid+=1; m2id[k]=str(nid)
for k,t in T.items():
    if path(k) in p2id: continue
    O['transforms'].append(dict(id=m2id[k],name=GO[t['go']],parent=m2id.get(t['parent']),lp=t['lp'],lr=t['lr'],ls=t['ls']))
# 3) part areas
area={'wing1_L':22.0,'wing1_R':22.0,'wing2_L':10.0,'wing2_R':10.0,'elevator_L':10.0,'elevator_R':10.0}
for p in O['parts']:
    if p['name'] in area: p['wingArea']=area[p['name']]
# 4) canard parts + surfaces copied from the elevators
byname={p['name']:p for p in O['parts']}; sbyname={s['name']:s for s in O['surfaces']}
tid_of={GO[T[k]['go']]+'@'+path(k):m2id[k] for k in T}
def newtid(name, under):
    for k in T:
        if GO[T[k]['go']]==name and under in path(k): return m2id[k]
    raise KeyError(name)
for side in 'LR':
    cn=f'elevator_{side} (1)'; ctid=newtid(cn,cn)
    ep=copy.deepcopy(byname[f'elevator_{side}'])
    ep['name']=cn; ep['tid']=ctid; ep['wingArea']=15.0
    ep['liftNormal']=newtid(f'elevator_{side}_liftNormal',cn)
    for c in ep['colliders']: c['tid']=ctid
    O['parts'].append(ep)
    es=copy.deepcopy(sbyname[f'elevator_{side}'])
    es['name']=cn; es['tid']=ctid; es['attached']=ctid; es['visible']=newtid(f'elevator_{side}_visible',cn)
    es['pitchRange']=35.0; es['yawRange']=(-10.0 if side=='L' else 10.0)
    O['surfaces'].append(es)
O['parameters']=dict(O['parameters']); O['parameters']['aircraftName']='KR-67 MTD'
json.dump(O,open(__import__('sys').argv[3],'w'))
print(len(O['transforms']),len(O['parts']),len(O['surfaces']),[ (s['name'],s['pitchRange'],s['yawRange']) for s in O['surfaces'] if 'elevator' in s['name']])
