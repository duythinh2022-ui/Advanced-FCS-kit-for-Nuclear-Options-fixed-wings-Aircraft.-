import UnityPy, json, sys, pickle
D="/mnt/user-data/uploads/Nuclear Option/NuclearOption_Data"
env=UnityPy.load(D+"/resources.assets")
RA=[f for n,f in env.files.items() if n.endswith("resources.assets")][0]
print("unity",RA.unity_version, len(RA.objects))
objs=RA.objects
T={};GO={}
import collections
cnt=collections.Counter(o.type.name for o in objs.values()); print(cnt.most_common(30))
for o in objs.values():
    if o.type.name=="Transform":
        d=o.read_typetree()
        T[o.path_id]=dict(go=d["m_GameObject"]["m_PathID"],parent=d["m_Father"]["m_PathID"],p=d["m_LocalPosition"],q=d["m_LocalRotation"],s=d["m_LocalScale"],ch=[c["m_PathID"] for c in d["m_Children"]])
    elif o.type.name=="GameObject":
        d=o.read_typetree(); GO[o.path_id]=dict(name=d["m_Name"],comps=[c["component"]["m_PathID"] for c in d["m_Component"]],layer=d["m_Layer"],active=d["m_IsActive"])
pickle.dump((T,GO),open("/home/claude/work/geo/tg.pkl","wb"))
roots=[k for k,t in T.items() if t["parent"]==0 and GO[t["go"]]["name"].startswith("SmallFighter1")]
for r in roots: print(r, GO[T[r]["go"]]["name"], len(T[r]["ch"]))
