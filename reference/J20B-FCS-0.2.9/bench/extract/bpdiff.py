import UnityPy,sys,json,math
import numpy as np
def load(path):
    data=open(path,'rb').read(); env=UnityPy.load(data[data.find(b'UnityFS'):])
    O={o.path_id:o for o in env.objects}; T={}; GO={}
    for o in env.objects:
        if o.type.name=="Transform":
            d=o.read_typetree(); T[o.path_id]=d
        elif o.type.name=="GameObject":
            GO[o.path_id]=o.read_typetree()
    root=[k for k,t in T.items() if t["m_Father"]["m_PathID"]==0 and GO[t["m_GameObject"]["m_PathID"]]["m_Name"]=="J20NativeMinimum"][0]
    def q2m(q):
        x,y,z,w=q['x'],q['y'],q['z'],q['w']
        return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    parts={}
    def walk(t,R,P,S,path):
        d=T[t]; g=GO[d["m_GameObject"]["m_PathID"]]; n=g["m_Name"]
        lp=d["m_LocalPosition"]; ls=d["m_LocalScale"]
        P2=P+R@(S*np.array([lp['x'],lp['y'],lp['z']])); R2=R@q2m(d["m_LocalRotation"]); S2=S*np.array([ls['x'],ls['y'],ls['z']])
        comps={}
        for c in g["m_Component"]:
            o=O.get(c["component"]["m_PathID"])
            if o is None or o.type.name!="MonoBehaviour": continue
            try:
                td=o.read_typetree(); cls=o.read().m_Script.read().m_ClassName
            except Exception: continue
            comps[cls]=td
        parts[n]=dict(path=path,pos=P2,comps=comps)
        for c in d["m_Children"]:
            if c["m_PathID"] in T: walk(c["m_PathID"],R2,P2,S2,path+'/'+n)
    walk(root,np.eye(3),np.zeros(3),np.ones(3),'')
    return parts
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
A=load(sys.argv[1]); B=load(sys.argv[2])
import pickle; pickle.dump((A,B),open('/tmp/bp.pkl','wb'))
