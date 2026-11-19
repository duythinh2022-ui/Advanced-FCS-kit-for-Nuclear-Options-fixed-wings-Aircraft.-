# Su-30VXN Gandiva (kyleisred SolarSu30.dll, Blueprinter bundle cut at UnityFS, root "Su-30") -> bench airframe json
import UnityPy, json, sys, collections
SRC=sys.argv[1]; OUT=sys.argv[2]
env=UnityPy.load(SRC)
objs={o.path_id:o for o in env.objects}
scripts={o.path_id:o.read().m_ClassName for o in env.objects if o.type.name=="MonoScript"}
T={};GO={}
for o in env.objects:
    if o.type.name in ("Transform","RectTransform"):
        d=o.read_typetree()
        T[o.path_id]=dict(go=d["m_GameObject"]["m_PathID"],parent=d["m_Father"]["m_PathID"],lp=[d["m_LocalPosition"][k] for k in "xyz"],lr=[d["m_LocalRotation"][k] for k in "xyzw"],ls=[d["m_LocalScale"][k] for k in "xyz"],children=[c["m_PathID"] for c in d["m_Children"]])
    elif o.type.name=="GameObject":
        d=o.read_typetree(); GO[o.path_id]=dict(name=d["m_Name"],active=d.get("m_IsActive",1))
go2t={t["go"]:tid for tid,t in T.items()}
MB=collections.defaultdict(list)
for o in env.objects:
    if o.type.name!="MonoBehaviour": continue
    d=o.read_typetree(); cls=scripts.get(d["m_Script"]["m_PathID"],"?")
    d["_pathid"]=o.path_id; d["_go"]=GO.get(d["m_GameObject"]["m_PathID"],{"name":None})["name"]; MB[cls].append(d)
acmb=[a for a in MB["Aircraft"] if a["_go"]][0]; rootT=go2t[acmb["m_GameObject"]["m_PathID"]]
def walk(r):
    s=set(); st=[r]
    while st:
        t=st.pop(); s.add(t); st.extend(c for c in T[t]["children"] if c in T)
    return s
keep=walk(rootT)
tr=[dict(id=str(t),name=GO[T[t]["go"]]["name"],parent=str(T[t]["parent"]) if T[t]["parent"] in keep else None,lp=T[t]["lp"],lr=T[t]["lr"],ls=T[t]["ls"]) for t in keep]
def tid(goPid): return str(go2t[goPid])
coll=collections.defaultdict(list)
for o in env.objects:
    if o.type.name not in ("MeshCollider","BoxCollider","CapsuleCollider","SphereCollider"): continue
    d=o.read_typetree(); g=d["m_GameObject"]["m_PathID"]
    if go2t.get(g) not in keep: continue
    c=dict(type=o.type.name,tid=tid(g),trigger=bool(d.get("m_IsTrigger",0)),enabled=bool(d.get("m_Enabled",1)))
    if o.type.name=="BoxCollider": c.update(size=[d["m_Size"][k] for k in "xyz"],center=[d["m_Center"][k] for k in "xyz"])
    elif o.type.name=="CapsuleCollider": c.update(radius=d["m_Radius"],height=d["m_Height"],dir=d["m_Direction"],center=[d["m_Center"][k] for k in "xyz"])
    elif o.type.name=="SphereCollider": c.update(radius=d["m_Radius"],center=[d["m_Center"][k] for k in "xyz"])
    else:
        mp=d["m_Mesh"]["m_PathID"]
        if mp not in objs: c["nomesh"]=1
        else:
            m=objs[mp].read(); b=m.m_LocalAABB
            c.update(center=[b.m_Center.x,b.m_Center.y,b.m_Center.z],size=[2*b.m_Extent.x,2*b.m_Extent.y,2*b.m_Extent.z],mesh=m.m_Name)
    coll[c["tid"]].append(c)
aero={a["_pathid"]:a for a in MB["AeroPart"]}
partT={go2t[a["m_GameObject"]["m_PathID"]] for a in aero.values()}
def subtree(t,acc):
    acc.append(str(t))
    for c in T[t]["children"]:
        if c in partT or c not in T: continue
        subtree(c,acc)
out=dict(root=str(rootT),prefab=GO[T[rootT]["go"]]["name"],transforms=tr,parts=[],surfaces=[],engines=[],nozzles=[],hlds=[],responders=[],tanks=[],gears=[],airbrakes=[])
ap2t={}
for k,a in aero.items():
    t=go2t[a["m_GameObject"]["m_PathID"]]; ap2t[k]=str(t)
    if t not in keep: print("part outside root:",a["_go"]); continue
    ln=a["liftNormal"]["m_PathID"]; acc=[]; subtree(t,acc)
    out["parts"].append(dict(name=a["_go"],tid=str(t),mass=a["mass"],wingArea=a["wingArea"],dragArea=a["dragArea"],airfoil=a["airfoil"],col=[a["centerOfLift"][q] for q in "xyz"],liftNormal=str(ln) if ln in T else str(t),channel=a["airflowChanneling"],streamlining=a.get("streamlining"),colliders=[c for x in acc for c in coll.get(x,[])]))
skippedCS=[]
for s in MB["ControlSurface"]:
    # Su-30: the gear-driven landing flaps (flap = 1, on Flaperon_*_LandingBias) and the dead outer flaps (all ranges 0) are not FCS
    # effectors; the FCS leaves them to the game (landing bias) or fixed (outer flaps), so the bench leaves them out
    if s.get("flap",0) or (s["pitchRange"]==0 and s["rollRange"]==0 and s["yawRange"]==0):
        skippedCS.append((s["_go"],s.get("flap",0))); continue
    vm=s["visibleMesh"]["m_PathID"]
    out["surfaces"].append(dict(name=s["_go"],tid=tid(s["m_GameObject"]["m_PathID"]),visible=str(go2t[vm]) if vm in go2t else None,attached=ap2t[s["attachedSurface"]["m_PathID"]],pitchRange=s["pitchRange"],rollRange=s["rollRange"],yawRange=s["yawRange"],servo=s["servoSpeed"],brakeRange=s["brakeRange"],maxSplit=s["maxSplit"],splitDrag=s["splitDrag"],yawSplitFactor=s["yawSplitFactor"],flap=s["flap"]))
jn={}
for n in MB["JetNozzle"]:
    jn[n["_pathid"]]=dict(tid=tid(n["m_GameObject"]["m_PathID"]),thrustT=str(n["thrustTransform"]["m_PathID"]),ab=[a["thrust"] for a in n["afterburners"]],abStart=[a["throttleStart"] for a in n["afterburners"]],part=ap2t.get(n["part"]["m_PathID"]))
    out["nozzles"].append(jn[n["_pathid"]])
for e in MB["Turbojet"]:
    out["engines"].append(dict(name=e["_go"],tid=tid(e["m_GameObject"]["m_PathID"]),kind="turbojet",staticThrust=e["maxThrust"],vec=[e["thrustVectoring"][q] for q in "xyz"],gain=[e["thrustVectoringGain"][q] for q in "xyz"],vecMaxSpeed=e["thrustVectoringMaxAirspeed"],vecT=[str(x["m_PathID"]) for x in e["vectoringTransforms"]],nozzles=[jn[x["m_PathID"]]["tid"] for x in e["nozzles"]],speedThrust=[],
        remap=[e["throttleRemap"]["x"],e["throttleRemap"]["y"]],minRPM=e.get("minRPM"),maxRPM=e.get("maxRPM"),spool=e.get("spoolRate"),maxSpeed=e.get("maxSpeed"),fuelMin=e.get("fuelConsumptionMin"),fuelMax=e.get("fuelConsumptionMax"),alt=[[c["time"],c["value"],c["inSlope"],c["outSlope"]] for c in e["altitudeThrust"]["m_Curve"]]))
for h in MB["HighLiftDevice"]:
    out["hlds"].append(dict(tid=tid(h["m_GameObject"]["m_PathID"]),part=ap2t[h["aeroPart"]["m_PathID"]],speedDeployed=h["speedDeployed"],speedRetracted=h["speedRetracted"],deployedArea=h["deployedArea"],alphaMin=h.get("alphaMin",0.0),alphaFactor=h.get("alphaFactor",0.0),moving=[dict(tid=str(mp["transform"]["m_PathID"]),move=mp["move"],rotate=mp["rotate"],pR=[mp["positionRetracted"][q] for q in "xyz"],pD=[mp["positionDeployed"][q] for q in "xyz"],aR=[mp["anglesRetracted"][q] for q in "xyz"],aD=[mp["anglesDeployed"][q] for q in "xyz"]) for mp in h["movingParts"]]))
for x in MB["FuelTank"]: out["tanks"].append(dict(tid=tid(x["m_GameObject"]["m_PathID"]),capacity=x["fuelCapacity"]))
for x in MB["LandingGear"]: out["gears"].append(tid(x["m_GameObject"]["m_PathID"]))
def curve(c): return dict(keys=[[k["time"],k["value"],k["inSlope"],k["outSlope"],k.get("weightedMode",0),k.get("inWeight",0.333),k.get("outWeight",0.333)] for k in c["m_Curve"]],pre=c["m_PreInfinity"],post=c["m_PostInfinity"])
P=MB["AircraftParameters"][0]
out["airfoils"]=[dict(name=a["name"],lift=curve(a["liftCoef"]),drag=curve(a["dragCoef"])) for a in P["airfoils"]]
out["parameters"]={k:v for k,v in P.items() if not isinstance(v,(dict,list))}
fbw=MB["ControlsFilter"][0]["flyByWire"]; out["stockFbw"]={k:v for k,v in fbw.items() if not isinstance(v,dict)}
out["aoaLimiter"]={"threshold":5.0,"limit":fbw["alphaLimiter"]}
out["airbrakes"]=[dict(part=ap2t.get(x["part"]["m_PathID"]),dragAmount=x["dragAmount"],maxAngle=x["maxAngle"],openSpeed=x["openSpeed"]) for x in MB["Airbrake"]]
out["canardSurfaces"]=[dict(go=x["_go"],surface=str(x["surface"]["m_PathID"]),maxAngle=x["maxAngle"],minAngle=x["minAngle"],speed=x["responseSpeed"]) for x in MB["CanardSurface"]]
json.dump(out,open(OUT,"w"),indent=0)
print(len(tr),len(out["parts"]),sum(len(p["colliders"]) for p in out["parts"]),len(out["surfaces"]),len(out["engines"]),len(out["tanks"]),sum(t["capacity"] for t in out["tanks"]))
print("mass parts",sum(p["mass"] for p in out["parts"]))
print("no colliders:",[p["name"] for p in out["parts"] if not p["colliders"]])
print("skipped CS:",skippedCS)
print("hld parts:",[ [p["name"] for p in out["parts"] if p["tid"]==h["part"]][0] for h in out["hlds"]])
