import pickle,sys,numpy as np
sys.path.insert(0,'/home/claude/scripts')
A,B=pickle.load(open('/tmp/bp.pkl','rb'))
def flat(d,pre=''):
    out={}
    if isinstance(d,dict):
        for k,v in d.items():
            if k in('m_GameObject','m_Script','m_EditorClassIdentifier','m_Enabled','m_EditorHideFlags','m_Name'): continue
            out.update(flat(v,pre+'.'+k))
    elif isinstance(d,list):
        if len(d)>40: out[pre+'[len]']=len(d); return out
        for i,v in enumerate(d): out.update(flat(v,f'{pre}[{i}]'))
    else: out[pre]=d
    return out
aero=lambda P:{n:p for n,p in P.items() if any(c in p['comps'] for c in ('AeroPart','UnitPart'))}
print('classes4',sorted({c for p in A.values() for c in p['comps']}))
print('classes5',sorted({c for p in B.values() for c in p['comps']}))
for n in sorted(set(A)|set(B)):
    a=A.get(n); b=B.get(n)
    if not a or not b:
        if (a or b)['comps']: print('ONLY', '4' if a else '5', n, list((a or b)['comps']))
        continue
    lines=[]
    if a['path']!=b['path'] and (a['comps'] or b['comps']): lines.append(f'  parent {a["path"]} -> {b["path"]}')
    dp=b['pos']-a['pos']
    if (a['comps'] or b['comps']) and np.abs(dp).max()>1e-3: lines.append(f'  worldpos {np.round(a["pos"],3)} -> {np.round(b["pos"],3)}')
    for c in set(a['comps'])|set(b['comps']):
        if c not in a['comps'] or c not in b['comps']: lines.append(f'  comp {c} only in {"4" if c in a["comps"] else "5"}'); continue
        fa=flat(a['comps'][c]); fb=flat(b['comps'][c])
        for k in sorted(set(fa)|set(fb)):
            if 'PathID' in k or 'FileID' in k: continue
            x=fa.get(k); y=fb.get(k)
            if isinstance(x,float) and isinstance(y,float):
                if abs(x-y)>1e-5*max(1,abs(x)): lines.append(f'  {c}{k}: {x:.5g} -> {y:.5g}')
            elif x!=y: lines.append(f'  {c}{k}: {str(x)[:60]} -> {str(y)[:60]}')
    if lines: print(n); print('\n'.join(lines[:30]))
