import UnityPy, json, numpy as np
env=UnityPy.load("bundle.bin")
objs={o.path_id:o for o in env.objects}
T={}   # transform pathid -> dict
GO={}  # gameobject pathid -> dict
for o in env.objects:
    if o.type.name in ("Transform","RectTransform"):
        d=o.read_typetree()
        T[o.path_id]=dict(go=d["m_GameObject"]["m_PathID"],parent=d["m_Father"]["m_PathID"],
            p=[d["m_LocalPosition"][k] for k in "xyz"],q=[d["m_LocalRotation"][k] for k in "xyzw"],s=[d["m_LocalScale"][k] for k in "xyz"],
            children=[c["m_PathID"] for c in d["m_Children"]])
    elif o.type.name=="GameObject":
        d=o.read_typetree()
        GO[o.path_id]=dict(name=d["m_Name"],comps=[c["component"]["m_PathID"] for c in d["m_Component"]],active=d.get("m_IsActive",1))
go2t={}
for tid,t in T.items(): go2t[t["go"]]=tid
def qmul(a,b):
    x1,y1,z1,w1=a;x2,y2,z2,w2=b
    return [w1*x2+x1*w2+y1*z2-z1*y2, w1*y2-x1*z2+y1*w2+z1*x2, w1*z2+x1*y2-y1*x2+z1*w2, w1*w2-x1*x2-y1*y2-z1*z2]
def qrot(q,v):
    x,y,z,w=q; u=np.array([x,y,z]); v=np.array(v)
    return 2*np.dot(u,v)*u+(w*w-np.dot(u,u))*v+2*w*np.cross(u,v)
W={}
def world(tid):
    if tid in W: return W[tid]
    t=T[tid]
    if t["parent"]==0 or t["parent"] not in T:
        r=(np.array(t["p"]),t["q"],np.array(t["s"]))
    else:
        pp,pq,ps=world(t["parent"])
        pos=pp+qrot(pq,ps*np.array(t["p"]))
        r=(pos,qmul(pq,t["q"]),ps*np.array(t["s"]))
    W[tid]=r; return r
# find root
root=[tid for tid,t in T.items() if GO.get(t["go"],{}).get("name")=="Aryx_KingRaptor" ]
print("root candidates",[(r,T[r]["parent"]) for r in root])
def tinfo(tid):
    if tid not in T: return None
    p,q,s=world(tid)
    return dict(name=GO[T[tid]["go"]]["name"],pos=p.tolist(),rot=list(q),scale=s.tolist(),path=path(tid))
def path(tid):
    n=[]
    while tid in T:
        n.append(GO[T[tid]["go"]]["name"]); tid=T[tid]["parent"]
    return "/".join(reversed(n))
# colliders
coll={}
for o in env.objects:
    if o.type.name in ("MeshCollider","BoxCollider","CapsuleCollider","SphereCollider"):
        d=o.read_typetree(); g=d["m_GameObject"]["m_PathID"]
        c=dict(type=o.type.name,enabled=d.get("m_Enabled",1),trigger=d.get("m_IsTrigger",0))
        if o.type.name=="BoxCollider": c.update(size=[d["m_Size"][k] for k in "xyz"],center=[d["m_Center"][k] for k in "xyz"])
        if o.type.name=="CapsuleCollider": c.update(radius=d["m_Radius"],height=d["m_Height"],dir=d["m_Direction"],center=[d["m_Center"][k] for k in "xyz"])
        if o.type.name=="MeshCollider":
            mid=d["m_Mesh"]["m_PathID"]; c["convex"]=d.get("m_Convex")
            if mid in objs:
                m=objs[mid].read()
                try:
                    b=m.m_LocalAABB; c.update(center=[b.m_Center.x,b.m_Center.y,b.m_Center.z],size=[2*b.m_Extent.x,2*b.m_Extent.y,2*b.m_Extent.z],mesh=m.m_Name)
                except Exception as e: c["err"]=str(e)
        c["t"]=tinfo(go2t[g]) if g in go2t else None
        coll.setdefault(g,[]).append(c)
mbs=json.load(open("mbs.json"))
def mbgo(pathid):
    # pathid of monobehaviour -> its gameobject transform id
    o=objs[pathid]; d=o.read_typetree(); return d["m_GameObject"]["m_PathID"]
out=dict(parts=[],surfaces=[],engines=[],nozzles=[],gears=[],hld=[],tanks=[])
for a in mbs["AeroPart"]:
    g=mbgo(a["_pathid"]); tid=go2t[g]
    ln=a["liftNormal"]["m_PathID"]
    out["parts"].append(dict(name=a["_go"],pathid=a["_pathid"],go=g,t=tinfo(tid),mass=a["mass"],wingArea=a["wingArea"],dragArea=a["dragArea"],airfoil=a["airfoil"],col=[a["centerOfLift"][k] for k in "xyz"],liftNormal=tinfo(ln) if ln in T else tinfo(tid),channel=a["airflowChanneling"],joints=[j["connectedPart"]["m_PathID"] for j in a["joints"]]))
for c in mbs["ControlSurface"]:
    d=objs[c["_pathid"]].read_typetree()
    vm=d["visibleMesh"]["m_PathID"]; att=d["attachedSurface"]["m_PathID"]
    out["surfaces"].append(dict(name=c["_go"],pitchRange=c["pitchRange"],rollRange=c["rollRange"],yawRange=c["yawRange"],servo=c["servoSpeed"],visible=tinfo(go2t[vm]) if vm in go2t else None,attached=att,brakeRange=c["brakeRange"],maxSplit=c["maxSplit"]))
for t in mbs["Turbofan"]:
    d=objs[t["_pathid"]].read_typetree()
    out["engines"].append(dict(name=t["_go"],staticThrust=t["staticThrust"],vec=[t["thrustVectoring"][k] for k in "xyz"],vecT=[tinfo(x["m_PathID"]) for x in d["vectoringTransforms"]],nozzles=[x["m_PathID"] for x in d["nozzles"]],keys={k:v for k,v in d.items() if not isinstance(v,(list,dict))}))
for n in mbs["JetNozzle"]:
    d=objs[n["_pathid"]].read_typetree()
    out["nozzles"].append(dict(pathid=n["_pathid"],name=n["_go"],thrustT=tinfo(d["thrustTransform"]["m_PathID"]),keys={k:v for k,v in d.items() if not isinstance(v,(list,dict))}))
for x in mbs["LandingGear"]: out["gears"].append(x["_go"])
for x in mbs["FuelTank"]:
    d=objs[x["_pathid"]].read_typetree(); out["tanks"].append({k:v for k,v in d.items() if not isinstance(v,(list,dict))}|{"go":x["_go"]})
for x in mbs["HighLiftDevice"]:
    d=objs[x["_pathid"]].read_typetree(); out["hld"].append({k:v for k,v in d.items() if not isinstance(v,(list,dict))}|{"go":x["_go"],"aeroPart":d["aeroPart"]["m_PathID"]})
# colliders per part: subtree excluding other parts
partgos={p["go"] for p in out["parts"]}
def subtree(tid,acc):
    acc.append(tid)
    for c in T[tid]["children"]:
        if T[c]["go"] in partgos: continue
        subtree(c,acc)
for p in out["parts"]:
    acc=[]; subtree(go2t[p["go"]],acc)
    p["colliders"]=[c for tid in acc for c in coll.get(T[tid]["go"],[])]
out["airfoils"]=mbs["AircraftParameters"][0]["airfoils"]
out["aircraft"]={k:v for k,v in mbs["Aircraft"][0].items() if not isinstance(v,(list,dict))}
out["definition"]={k:v for k,v in mbs["AircraftDefinition"][0].items() if not isinstance(v,(list,dict))}
json.dump(out,open("airframe.json","w"),indent=1)
print(len(out["parts"]), sum(p["mass"] for p in out["parts"]))
for p in out["parts"]:
    print(f'{p["name"]:34s} pos={np.round(p["t"]["pos"],2)} ln={np.round(p["liftNormal"]["pos"],2)} ncol={len(p["colliders"])}')
