import json,numpy as np
exec(open('scripts/com.py').read().split("mp=json.load")[0])
a4=json.load(open('ext/a4.json')); T={t['id']:t for t in a4['transforms']}
mp=json.load(open('/tmp/res_5_Reasonix.J20.AeroControl.MassProperties069.json'))
def rot(n):
    p=[q for q in a4['parts'] if q['name']==n][0]; i=p['tid']; R=np.eye(3)
    while i is not None:
        t=T[i]; R=q2m(t['lr'])@R; i=t['parent']
    return R
for n in ['J20NativeMinimum','cockpit','fuselage_R','intake_L','intake_R','engine_L','engine_R']:
    print(n,'rot identity' if np.allclose(rot(n),np.eye(3),atol=1e-4) else rot(n).round(3), 'pivot',A[n]['pivot'].round(4),'pivot5',B[n]['pivot'].round(4))
res={}
for n in ['J20NativeMinimum','cockpit','fuselage_R']:
    res[n]=A[n]['com']-A[n]['pivot']
for s in 'LR':
    i=A['intake_'+s]; b=A['D25_SideBay_'+s]
    c=(i['mass']*i['com']+b['mass']*b['com'])/(i['mass']+b['mass'])
    res['intake_'+s]=c-i['pivot']
    print('intake',s,'0.0.4 intake com',i['com'].round(3),'sidebay com',b['com'].round(3),'combined',c.round(3),'mass',i['mass']+b['mass'])
e={x['name']:x for x in mp['parts']}
for n,v in res.items():
    cur=np.array(e[n]['localCenterOfMass']) if n in e else None
    print(f'{n:<17} 0.0.4 local {v.round(4)}   0.0.5 table local {None if cur is None else cur.round(4)}')
json.dump({k:v.tolist() for k,v in res.items()},open('/tmp/com04.json','w'))
