import UnityPy, json
env=UnityPy.load("bundle.bin")
objs={o.path_id:o for o in env.objects}
T={};GO={}
for o in env.objects:
    if o.type.name in ("Transform","RectTransform"):
        d=o.read_typetree()
        T[o.path_id]=dict(go=d["m_GameObject"]["m_PathID"],parent=d["m_Father"]["m_PathID"],lp=[d["m_LocalPosition"][k] for k in "xyz"],lr=[d["m_LocalRotation"][k] for k in "xyzw"],ls=[d["m_LocalScale"][k] for k in "xyz"],children=[c["m_PathID"] for c in d["m_Children"]])
    elif o.type.name=="GameObject":
        d=o.read_typetree(); GO[o.path_id]=dict(name=d["m_Name"])
go2t={t["go"]:tid for tid,t in T.items()}
cands=[tid for tid,t in T.items() if GO[t["go"]]["name"]=="Aryx_KingRaptor" and t["parent"]==0]
def walkset(r):
    s=set(); st=[r]
    while st:
        t=st.pop(); s.add(t); st.extend(T[t]["children"])
    return s
sets={r:walkset(r) for r in cands}
print({r:len(s) for r,s in sets.items()})
root=max(sets,key=lambda r:len(sets[r])); keep=sets[root]
tr=[dict(id=str(t),name=GO[T[t]["go"]]["name"],parent=str(T[t]["parent"]) if T[t]["parent"] in keep else None,lp=T[t]["lp"],lr=T[t]["lr"],ls=T[t]["ls"]) for t in keep]
def tidOfGo(g): return str(go2t[g])
def mbGo(pid): return objs[pid].read_typetree()["m_GameObject"]["m_PathID"]
coll={}
for o in env.objects:
    if o.type.name in ("MeshCollider","BoxCollider","CapsuleCollider","SphereCollider"):
        d=o.read_typetree(); g=d["m_GameObject"]["m_PathID"]
        if go2t.get(g) not in keep: continue
        c=dict(type=o.type.name,tid=tidOfGo(g),trigger=d.get("m_IsTrigger",0),enabled=d.get("m_Enabled",1))
        if o.type.name=="BoxCollider": c.update(size=[d["m_Size"][k] for k in "xyz"],center=[d["m_Center"][k] for k in "xyz"])
        elif o.type.name=="CapsuleCollider": c.update(radius=d["m_Radius"],height=d["m_Height"],dir=d["m_Direction"],center=[d["m_Center"][k] for k in "xyz"])
        elif o.type.name=="SphereCollider": c.update(radius=d["m_Radius"],center=[d["m_Center"][k] for k in "xyz"])
        else:
            m=objs[d["m_Mesh"]["m_PathID"]].read(); b=m.m_LocalAABB
            c.update(center=[b.m_Center.x,b.m_Center.y,b.m_Center.z],size=[2*b.m_Extent.x,2*b.m_Extent.y,2*b.m_Extent.z])
        coll.setdefault(tidOfGo(g),[]).append(c)
mbs=json.load(open("mbs.json"))
partT={str(go2t[mbGo(a["_pathid"])]) for a in mbs["AeroPart"]}
def subtree(tid,acc):
    acc.append(tid)
    for c in T[int(tid)]["children"]:
        if str(c) in partT: continue
        subtree(str(c),acc)
out=dict(root=str(root),transforms=tr,parts=[],surfaces=[],engines=[],nozzles=[],hlds=[],responders=[],tanks=[],gears=[])
ap2t={}
for a in mbs["AeroPart"]:
    d=objs[a["_pathid"]].read_typetree(); tid=str(go2t[d["m_GameObject"]["m_PathID"]]); ap2t[a["_pathid"]]=tid
    ln=d["liftNormal"]["m_PathID"]
    acc=[];subtree(tid,acc)
    out["parts"].append(dict(name=a["_go"],tid=tid,mass=d["mass"],wingArea=d["wingArea"],dragArea=d["dragArea"],airfoil=d["airfoil"],col=[d["centerOfLift"][k] for k in "xyz"],liftNormal=str(ln) if ln in T else tid,channel=d["airflowChanneling"],colliders=[c for t in acc for c in coll.get(t,[])]))
for s in mbs["ControlSurface"]:
    d=objs[s["_pathid"]].read_typetree()
    out["surfaces"].append(dict(name=s["_go"],tid=str(go2t[d["m_GameObject"]["m_PathID"]]),visible=str(go2t[d["visibleMesh"]["m_PathID"]]),attached=ap2t[d["attachedSurface"]["m_PathID"]],pitchRange=d["pitchRange"],rollRange=d["rollRange"],yawRange=d["yawRange"],servo=d["servoSpeed"],brakeRange=d["brakeRange"],maxSplit=d["maxSplit"]))
jn={}
for n in mbs["JetNozzle"]:
    d=objs[n["_pathid"]].read_typetree()
    jn[n["_pathid"]]=dict(tid=str(go2t[d["m_GameObject"]["m_PathID"]]),thrustT=str(d["thrustTransform"]["m_PathID"]),ab=[a["thrust"] for a in d["afterburners"]],part=ap2t.get(d["part"]["m_PathID"]))
    out["nozzles"].append(jn[n["_pathid"]])
for t in mbs["Turbofan"]:
    d=objs[t["_pathid"]].read_typetree()
    out["engines"].append(dict(name=t["_go"],tid=str(go2t[d["m_GameObject"]["m_PathID"]]),staticThrust=d["staticThrust"],vec=[d["thrustVectoring"][k] for k in "xyz"],gain=[d["thrustVectoringGain"][k] for k in "xyz"],vecMaxSpeed=d["thrustVectoringMaxAirspeed"],vecT=[str(x["m_PathID"]) for x in d["vectoringTransforms"]],nozzles=[jn[x["m_PathID"]]["tid"] for x in d["nozzles"]],speedThrust=[[c["time"],c["value"]] for c in d["speedThrust"]["m_Curve"]]))
for h in mbs["HighLiftDevice"]:
    d=objs[h["_pathid"]].read_typetree()
    out["hlds"].append(dict(tid=str(go2t[d["m_GameObject"]["m_PathID"]]),part=ap2t[d["aeroPart"]["m_PathID"]],speedDeployed=d["speedDeployed"],speedRetracted=d["speedRetracted"],deployedArea=d["deployedArea"],alphaMin=d["alphaMin"],alphaFactor=d["alphaFactor"],moving=[dict(tid=str(mp["transform"]["m_PathID"]),move=mp["move"],rotate=mp["rotate"],pR=[mp["positionRetracted"][k] for k in "xyz"],pD=[mp["positionDeployed"][k] for k in "xyz"],aR=[mp["anglesRetracted"][k] for k in "xyz"],aD=[mp["anglesDeployed"][k] for k in "xyz"]) for mp in d["movingParts"]]))
for r in mbs["AryxAlphaResponder"]:
    d=objs[r["_pathid"]].read_typetree()
    out["responders"].append(dict(tid=str(go2t[d["m_GameObject"]["m_PathID"]]),rotator=str(d["rotator"]["m_PathID"])))
for x in mbs["FuelTank"]:
    d=objs[x["_pathid"]].read_typetree()
    out["tanks"].append(dict(tid=str(go2t[d["m_GameObject"]["m_PathID"]]),capacity=d["fuelCapacity"]))
for x in mbs["LandingGear"]:
    d=objs[x["_pathid"]].read_typetree(); out["gears"].append(str(go2t[d["m_GameObject"]["m_PathID"]]))
def curve(c): return dict(keys=[[k["time"],k["value"],k["inSlope"],k["outSlope"],k.get("weightedMode",0),k.get("inWeight",0.333),k.get("outWeight",0.333)] for k in c["m_Curve"]],pre=c["m_PreInfinity"],post=c["m_PostInfinity"])
out["airfoils"]=[dict(name=a["name"],lift=curve(a["liftCoef"]),drag=curve(a["dragCoef"])) for a in mbs["AircraftParameters"][0]["airfoils"]]
json.dump(out,open("bench_airframe.json","w"))
print(len(tr), len(out["parts"]), sum(len(p["colliders"]) for p in out["parts"]))
print(set(k[4] for a in out["airfoils"] for c in (a["lift"],a["drag"]) for k in c["keys"]))
missing=[p["name"] for p in out["parts"] if not p["colliders"]]; print("no colliders:",missing)
