import UnityPy, json, sys, pickle, numpy as np
D="/mnt/user-data/uploads/Nuclear Option/NuclearOption_Data"
env=UnityPy.load(D+"/resources.assets")
RA=[f for n,f in env.files.items() if n.endswith("resources.assets")][0]
objs=RA.objects
T,GO=pickle.load(open("/home/claude/work/geo/tg.pkl","rb"))
root=28208
def qmat(q):
    x,y,z,w=q["x"],q["y"],q["z"],q["w"]
    return np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
def local(t):
    M=np.eye(4); M[:3,:3]=qmat(t["q"])@np.diag([t["s"]["x"],t["s"]["y"],t["s"]["z"]]); M[:3,3]=[t["p"]["x"],t["p"]["y"],t["p"]["z"]]; return M
W={}
order=[]
def walk(tid,M,depth,path):
    t=T[tid]; Mw=M@local(t) if tid!=root else np.eye(4)
    W[tid]=Mw; name=GO[t["go"]]["name"]; order.append((tid,depth,path+"/"+name))
    for c in t["ch"]:
        if c in T: walk(c,Mw,depth+1,path+"/"+name)
walk(root,np.eye(4),0,"")
print(len(order))
info={}
for tid,depth,path in order:
    g=GO[T[tid]["go"]]; rec=dict(tid=tid,depth=depth,path=path,name=g["name"],active=g["active"],layer=g["layer"],M=W[tid].tolist(),lp=T[tid]["p"],lq=T[tid]["q"],ls=T[tid]["s"],comps=[])
    for cid in g["comps"]:
        o=objs.get(cid)
        if o is None: continue
        tn=o.type.name
        if tn=="Transform": continue
        c=dict(type=tn,id=cid)
        try:
            if tn=="MeshFilter":
                d=o.read_typetree(); c["mesh"]=d["m_Mesh"]["m_PathID"]; c["meshfile"]=d["m_Mesh"]["m_FileID"]
            elif tn in("MeshRenderer","SkinnedMeshRenderer"):
                d=o.read_typetree(); c["mats"]=[(m["m_FileID"],m["m_PathID"]) for m in d["m_Materials"]]; c["enabled"]=d["m_Enabled"]; c["shadow"]=d.get("m_CastShadows"); c["recv"]=d.get("m_ReceiveShadows")
                if tn=="SkinnedMeshRenderer": c["mesh"]=d["m_Mesh"]["m_PathID"]
            elif tn=="MeshCollider":
                d=o.read_typetree(); c["mesh"]=d["m_Mesh"]["m_PathID"]; c["convex"]=d["m_Convex"]; c["enabled"]=d["m_Enabled"]
            elif tn=="LODGroup":
                d=o.read_typetree(); c["lod"]=d
            elif tn in("BoxCollider","CapsuleCollider","SphereCollider"):
                d=o.read_typetree(); c["d"]={k:v for k,v in d.items() if k not in("m_GameObject",)}
            elif tn=="MonoBehaviour":
                pass
        except Exception as e: c["err"]=str(e)[:80]
        rec["comps"].append(c)
    info[tid]=rec
pickle.dump(info,open("/home/claude/work/geo/vortex_h.pkl","wb"))
for tid,depth,path in order:
    r=info[tid]; cs=",".join(c["type"][:6]+(":"+str(c.get("mesh")) if "mesh" in c else "") for c in r["comps"] if c["type"]!="MonoBehaviour")
    p=np.array(r["M"])[:3,3]
    print("  "*depth+r["name"], "[%.3f %.3f %.3f]"%tuple(p), cs, "" if r["active"] else "(inactive)")
