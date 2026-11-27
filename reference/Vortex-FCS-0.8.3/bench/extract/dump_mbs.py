# dump every MonoBehaviour in bundle.bin (cut from the Aryx DLL at the UnityFS signature) by script class -> mbs.json; run before extract_mbs.py / extract2.py (2.1.0)
import UnityPy, json, sys
env=UnityPy.load("bundle.bin")
objs={o.path_id:o for o in env.objects}
out={}; fail={}
for o in env.objects:
    if o.type.name!="MonoBehaviour": continue
    try: d=o.read_typetree()
    except Exception as e:
        fail[str(e)[:60]]=fail.get(str(e)[:60],0)+1; continue
    sid=d["m_Script"]["m_PathID"]
    try:
        cls=objs[sid].read_typetree()["m_ClassName"] if sid in objs else None
    except Exception: cls=None
    if not cls: continue
    g=d["m_GameObject"]["m_PathID"]
    gname=objs[g].read_typetree()["m_Name"] if g in objs else None
    d["_pathid"]=o.path_id; d["_go"]=gname
    out.setdefault(cls,[]).append(d)
print({k:len(v) for k,v in out.items()}); print(fail)
def conv(x):
    if isinstance(x,dict): return {k:conv(v) for k,v in x.items()}
    if isinstance(x,list): return [conv(v) for v in x]
    if isinstance(x,(bytes,bytearray)): return x.hex()
    return x
json.dump(conv(out),open("mbs.json","w"))
