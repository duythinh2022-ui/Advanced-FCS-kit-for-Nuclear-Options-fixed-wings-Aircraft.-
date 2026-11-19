import UnityPy,json,collections
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
D="/mnt/user-data/uploads/Nuclear Option/NuclearOption_Data"
env=UnityPy.load(D+"/resources.assets",D+"/globalgamemanagers.assets",D+"/globalgamemanagers")
RA=[f for n,f in env.files.items() if n.endswith("resources.assets")][0]
g=TypeTreeGenerator(RA.unity_version); g.load_local_dll_folder(D+"/Managed"); env.typetree_generator=g
O=RA.objects
raw=json.load(open("/root/w/ifrit/ifrit_raw.json"))
T={int(k):v for k,v in raw["T"].items()}; GO={int(k):v for k,v in raw["GO"].items()}
MB={int(k):v for k,v in raw["MB"].items()}; OTH={int(k):v for k,v in raw["other"].items()}
go2t={t["go"]:k for k,t in T.items()}
root=go2t[raw["root"]]
keep=set(T)
def V(d,keys="xyz"): return [d[k] for k in keys]
tr=[dict(id=str(t),name=GO[T[t]["go"]]["name"],parent=str(T[t]["parent"]) if T[t]["parent"] in keep else None,lp=V(T[t]["p"]),lr=V(T[t]["q"],"xyzw"),ls=V(T[t]["s"])) for t in keep]
def has(m,*ks): return all(k in m for k in ks)
aero={k:m for k,m in MB.items() if has(m,"wingArea","liftNormal","airfoil")}
cs={k:m for k,m in MB.items() if has(m,"pitchRange","yawSplitFactor")}
eng={k:m for k,m in MB.items() if has(m,"maxThrust","thrustVectoring")}
noz={k:m for k,m in MB.items() if has(m,"turbojet","afterburners")}
hld={k:m for k,m in MB.items() if has(m,"speedDeployed","movingParts")}
tank={k:m for k,m in MB.items() if has(m,"fuelCapacity")}
gear={k:m for k,m in MB.items() if has(m,"suspensionTravel")}
partT={go2t[m["_go"]] for m in aero.values()}
coll=collections.defaultdict(list)
for cid,c in OTH.items():
    ty=c["_type"]
    if ty not in("MeshCollider","BoxCollider","CapsuleCollider","SphereCollider"): continue
    t=go2t[c["_go"]]
    e=dict(type=ty,tid=str(t),trigger=c.get("m_IsTrigger",0),enabled=c.get("m_Enabled",1))
    if ty=="BoxCollider": e.update(size=V(c["m_Size"]),center=V(c["m_Center"]))
    elif ty=="CapsuleCollider": e.update(radius=c["m_Radius"],height=c["m_Height"],dir=c["m_Direction"],center=V(c["m_Center"]))
    elif ty=="SphereCollider": e.update(radius=c["m_Radius"],center=V(c["m_Center"]))
    else:
        mid=c["m_Mesh"]["m_PathID"]
        if mid not in O: e["nomesh"]=1; coll[str(t)].append(e); continue
        m=O[mid].read(); b=m.m_LocalAABB
        e.update(center=[b.m_Center.x,b.m_Center.y,b.m_Center.z],size=[2*b.m_Extent.x,2*b.m_Extent.y,2*b.m_Extent.z],mesh=m.m_Name)
    coll[str(t)].append(e)
def subtree(tid,acc):
    acc.append(str(tid))
    for c in T[tid]["ch"]:
        if c in partT or c not in T: continue
        subtree(c,acc)
out=dict(root=str(root),transforms=tr,parts=[],surfaces=[],engines=[],nozzles=[],hlds=[],responders=[],tanks=[],gears=[])
ap2t={}
for k,m in aero.items():
    tid=go2t[m["_go"]]; ap2t[k]=str(tid); ln=m["liftNormal"]["m_PathID"]
    acc=[];subtree(tid,acc)
    out["parts"].append(dict(name=GO[m["_go"]]["name"],tid=str(tid),mass=m["mass"],wingArea=m["wingArea"],dragArea=m["dragArea"],airfoil=m["airfoil"],col=V(m["centerOfLift"]),liftNormal=str(ln) if ln in T else str(tid),channel=m["airflowChanneling"],streamlining=m.get("streamlining"),colliders=[c for t in acc for c in coll.get(t,[])]))
for k,m in cs.items():
    out["surfaces"].append(dict(name=GO[m["_go"]]["name"],tid=str(go2t[m["_go"]]),visible=str(go2t[m["visibleMesh"]["m_PathID"]]) if m["visibleMesh"]["m_PathID"] in go2t else None,attached=ap2t[m["attachedSurface"]["m_PathID"]],pitchRange=m["pitchRange"],rollRange=m["rollRange"],yawRange=m["yawRange"],servo=m["servoSpeed"],brakeRange=m["brakeRange"],maxSplit=m["maxSplit"],splitDrag=m["splitDrag"],yawSplitFactor=m["yawSplitFactor"],flap=m["flap"]))
jn={}
for k,m in noz.items():
    jn[k]=dict(tid=str(go2t[m["_go"]]),thrustT=str(m["thrustTransform"]["m_PathID"]),ab=[a["thrust"] for a in m["afterburners"]],abStart=[a["throttleStart"] for a in m["afterburners"]],part=ap2t.get(m["part"]["m_PathID"]))
    out["nozzles"].append(jn[k])
for k,m in eng.items():
    out["engines"].append(dict(name=GO[m["_go"]]["name"],tid=str(go2t[m["_go"]]),kind="turbojet",staticThrust=m["maxThrust"],vec=V(m["thrustVectoring"]),gain=V(m["thrustVectoringGain"]),vecMaxSpeed=m["thrustVectoringMaxAirspeed"],vecT=[str(x["m_PathID"]) for x in m["vectoringTransforms"]],nozzles=[jn[x["m_PathID"]]["tid"] for x in m["nozzles"]],speedThrust=[[0,1],[m["maxSpeed"],1],[m["maxSpeed"]*1.2,0]],
        remap=[m["throttleRemap"]["x"],m["throttleRemap"]["y"]],minRPM=m["minRPM"],maxRPM=m["maxRPM"],spool=m["spoolRate"],maxSpeed=m["maxSpeed"],alt=[[c["time"],c["value"],c["inSlope"],c["outSlope"]] for c in m["altitudeThrust"]["m_Curve"]]))
for k,m in hld.items():
    out["hlds"].append(dict(tid=str(go2t[m["_go"]]),part=ap2t[m["aeroPart"]["m_PathID"]],speedDeployed=m["speedDeployed"],speedRetracted=m["speedRetracted"],deployedArea=m["deployedArea"],alphaMin=m["alphaMin"],alphaFactor=m["alphaFactor"],moving=[dict(tid=str(mp["transform"]["m_PathID"]),move=mp["move"],rotate=mp["rotate"],pR=V(mp["positionRetracted"]),pD=V(mp["positionDeployed"]),aR=V(mp["anglesRetracted"]),aD=V(mp["anglesDeployed"])) for mp in m["movingParts"]]))
for k,m in tank.items(): out["tanks"].append(dict(tid=str(go2t[m["_go"]]),capacity=m["fuelCapacity"]))
for k,m in gear.items(): out["gears"].append(str(go2t[m["_go"]]))
p=O[62241].read_typetree()
def curve(c): return dict(keys=[[k["time"],k["value"],k["inSlope"],k["outSlope"],k.get("weightedMode",0),k.get("inWeight",0.333),k.get("outWeight",0.333)] for k in c["m_Curve"]],pre=c["m_PreInfinity"],post=c["m_PostInfinity"])
out["airfoils"]=[dict(name=a["name"],lift=curve(a["liftCoef"]),drag=curve(a["dragCoef"])) for a in p["airfoils"]]
fbw=[m for m in MB.values() if "flyByWire" in m][0]["flyByWire"]; out["stockFbw"]={k:v for k,v in fbw.items() if not isinstance(v,dict)}
out["aoaLimiter"]=[m for m in MB.values() if "aoaLimiter" in m][0]["aoaLimiter"]
json.dump(out,open("/root/w/ifrit/ifrit_airframe.json","w"),indent=0)
print(len(tr),len(out["parts"]),sum(len(p["colliders"]) for p in out["parts"]),len(out["surfaces"]),len(out["engines"]),len(out["tanks"]),sum(t["capacity"] for t in out["tanks"]))
print("no colliders:",[p["name"] for p in out["parts"] if not p["colliders"]])
