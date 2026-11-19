import UnityPy,json,sys,os
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
D="/mnt/user-data/uploads/Nuclear Option/NuclearOption_Data"
env=UnityPy.load(D+"/resources.assets",D+"/globalgamemanagers.assets",D+"/globalgamemanagers")
RA=[f for n,f in env.files.items() if n.endswith("resources.assets")][0]
ver=RA.unity_version
print("unity",ver)
g=TypeTreeGenerator(ver)
g.load_local_dll_folder(D+"/Managed")
env.typetree_generator=g
objs={o.path_id:o for o in RA.objects.values()}
T={};GO={}
for o in RA.objects.values():
    if o.type.name in("Transform",):
        d=o.read_typetree()
        T[o.path_id]=dict(go=d["m_GameObject"]["m_PathID"],parent=d["m_Father"]["m_PathID"],p=d["m_LocalPosition"],q=d["m_LocalRotation"],s=d["m_LocalScale"],ch=[c["m_PathID"] for c in d["m_Children"]])
    elif o.type.name=="GameObject":
        d=o.read_typetree(); GO[o.path_id]=dict(name=d["m_Name"],comps=[c["component"]["m_PathID"] for c in d["m_Component"]])
go2t={t["go"]:k for k,t in T.items()}
root=10605 if 10605 in go2t else 16713
for r in (10605,16713):
    print(r, GO[r]["name"], r in go2t, len(GO[r]["comps"]))
# collect subtree
def sub(tid,acc):
    acc.append(tid)
    for c in T[tid]["ch"]:
        if c in T: sub(c,acc)
    return acc
best=None
for r in (10605,16713):
    if r in go2t:
        s=sub(go2t[r],[]); print(r,"subtree",len(s))
        if best is None or len(s)>len(best[1]): best=(r,s)
r,s=best
gos=set(T[t]["go"] for t in s)
out={"root":r,"T":{str(k):T[k] for k in s},"GO":{str(k):GO[k] for k in gos},"MB":{},"other":{}}
fails={}
for gid in gos:
    for cid in GO[gid]["comps"]:
        o=objs.get(cid)
        if o is None: continue
        if o.type.name=="MonoBehaviour":
            try:
                d=o.read_typetree()
                sid=d["m_Script"]["m_PathID"]; fid=d["m_Script"]["m_FileID"]
                cls=None
                try:
                    sc=o.assets_file.objects.get(sid) if fid==0 else None
                    if sc is None:
                        sc=o.assets_file.externals and None
                    cls=sc.read_typetree()["m_ClassName"] if sc else None
                except Exception as e: cls=None
                if cls is None:
                    try: cls=o.read().m_Script.deref_parse_as_object().m_ClassName
                    except Exception as e:
                        try: cls=o.read().m_Script.read().m_ClassName
                        except Exception as e2: cls="?"+str(e2)[:40]
                d["_cls"]=cls; d["_go"]=gid; d["_id"]=cid
                out["MB"][str(cid)]=d
            except Exception as e:
                fails[str(e)[:80]]=fails.get(str(e)[:80],0)+1
        elif o.type.name not in ("Transform","MeshRenderer","MeshFilter","LODGroup"):
            try:
                d=o.read_typetree(); d["_type"]=o.type.name; d["_go"]=gid
                out["other"][str(cid)]=d
            except Exception as e: fails[str(e)[:80]]=fails.get(str(e)[:80],0)+1
print("fails",fails)
def conv(x):
    if isinstance(x,dict): return {str(k):conv(v) for k,v in x.items()}
    if isinstance(x,(list,tuple)): return [conv(v) for v in x]
    if isinstance(x,(bytes,bytearray)): return x.hex()
    return x
json.dump(conv(out),open(os.path.expanduser("~/w/ifrit/ifrit_raw.json"),"w"))
import collections
print(collections.Counter(m["_cls"] for m in out["MB"].values()).most_common(60))
