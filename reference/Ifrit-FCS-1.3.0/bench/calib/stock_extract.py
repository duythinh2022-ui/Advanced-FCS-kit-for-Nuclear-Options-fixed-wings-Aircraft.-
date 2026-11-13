#!/usr/bin/env python3
# Stock-jet airframe extraction from the game's resources.assets (bench calibration, 31 Oct 2026).
# Replaces the version-specific dump.py / build_json.py pair (hard-coded root and AircraftParameters path ids, which
# the 30 Oct game update renumbered): the aircraft prefab is found by NAME among the top-level GameObjects that carry an
# Aircraft component, its AircraftParameters is followed from the Aircraft's own `parameters` reference, and the
# MonoBehaviour class is resolved through the script reference.
#   stock_extract.py <prefab name, e.g. Fighter1 | Multirole1> <out.json>
# Collider meshes stream from resources.assets.resS (1 GB) which is not staged, so MeshColliders keep the AABB box
# approximation (no convex-hull mass properties for the stock jets).
import UnityPy, json, sys, collections, warnings
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
NAME, OUT = sys.argv[1], sys.argv[2]
D = "/mnt/user-data/uploads/Nuclear Option/NuclearOption_Data"
env = UnityPy.load(D + "/resources.assets", D + "/globalgamemanagers.assets", D + "/globalgamemanagers")
RA = [f for n, f in env.files.items() if n.endswith("resources.assets")][0]
g = TypeTreeGenerator(RA.unity_version); g.load_local_dll_folder(D + "/Managed"); env.typetree_generator = g
O = RA.objects
T = {}; GO = {}
for o in O.values():
    if o.type.name == "Transform":
        d = o.read_typetree(); T[o.path_id] = dict(go=d["m_GameObject"]["m_PathID"], parent=d["m_Father"]["m_PathID"], p=d["m_LocalPosition"], q=d["m_LocalRotation"], s=d["m_LocalScale"], ch=[c["m_PathID"] for c in d["m_Children"]])
    elif o.type.name == "GameObject":
        d = o.read_typetree(); GO[o.path_id] = dict(name=d["m_Name"], comps=[c["component"]["m_PathID"] for c in d["m_Component"]])
go2t = {t["go"]: k for k, t in T.items()}
# script class names (MonoScripts live in the other files; resolve through env.objects)
scripts = {}
for o in env.objects:
    if o.type.name == "MonoScript":
        try: scripts[o.path_id] = o.read_typetree()["m_ClassName"]
        except Exception: pass
def mb(cid):
    o = O.get(cid)
    if o is None or o.type.name != "MonoBehaviour": return None
    try:
        d = o.read_typetree()
    except Exception:
        return None
    d["_cls"] = scripts.get(d["m_Script"]["m_PathID"], "?")
    return d
# ---- root
roots = [(go, tid) for tid, t in T.items() if t["parent"] == 0 for go in [t["go"]] if GO[go]["name"] == NAME]
if not roots: raise SystemExit("prefab not found: " + NAME)
rootGo, rootT = roots[0]
def sub(tid, acc):
    acc.append(tid)
    for c in T[tid]["ch"]:
        if c in T: sub(c, acc)
    return acc
keep = sub(rootT, [])
gos = set(T[t]["go"] for t in keep)
MB = {}; OTH = {}
for gid in gos:
    for cid in GO[gid]["comps"]:
        o = O.get(cid)
        if o is None: continue
        if o.type.name == "MonoBehaviour":
            d = mb(cid)
            if d is not None: d["_go"] = gid; d["_id"] = cid; MB[cid] = d
        elif o.type.name not in ("Transform", "MeshRenderer", "MeshFilter", "LODGroup", "SkinnedMeshRenderer"):
            try:
                d = o.read_typetree(); d["_type"] = o.type.name; d["_go"] = gid; OTH[cid] = d
            except Exception: pass
print("root", NAME, "GO", rootGo, "transforms", len(keep), "MBs", len(MB), collections.Counter(m["_cls"] for m in MB.values()).most_common(25))
def V(d, keys="xyz"): return [d[k] for k in keys]
tr = [dict(id=str(t), name=GO[T[t]["go"]]["name"], parent=str(T[t]["parent"]) if T[t]["parent"] in set(keep) else None, lp=V(T[t]["p"]), lr=V(T[t]["q"], "xyzw"), ls=V(T[t]["s"])) for t in keep]
def has(m, *ks): return all(k in m for k in ks)
aero = {k: m for k, m in MB.items() if m["_cls"] == "AeroPart" or has(m, "wingArea", "liftNormal", "airfoil")}
cs = {k: m for k, m in MB.items() if m["_cls"] == "ControlSurface" or has(m, "pitchRange", "yawSplitFactor")}
eng = {k: m for k, m in MB.items() if m["_cls"] in ("Turbojet", "Turbofan") or has(m, "thrustVectoring", "spoolRate")}
noz = {k: m for k, m in MB.items() if m["_cls"] == "JetNozzle" or has(m, "afterburners", "thrustTransform")}
hld = {k: m for k, m in MB.items() if m["_cls"] == "HighLiftDevice" or has(m, "speedDeployed", "movingParts")}
tank = {k: m for k, m in MB.items() if m["_cls"] == "FuelTank" or has(m, "fuelCapacity")}
gear = {k: m for k, m in MB.items() if m["_cls"] == "LandingGear" or has(m, "suspensionTravel")}
abk = {k: m for k, m in MB.items() if m["_cls"] == "Airbrake" or has(m, "dragAmount", "maxAngle", "openSpeed")}
acs = [m for m in MB.values() if m["_cls"] == "Aircraft" or has(m, "partLookup", "parameters")]
partT = {go2t[m["_go"]] for m in aero.values()}
coll = collections.defaultdict(list)
for cid, c in OTH.items():
    ty = c["_type"]
    if ty not in ("MeshCollider", "BoxCollider", "CapsuleCollider", "SphereCollider"): continue
    t = go2t[c["_go"]]
    e = dict(type=ty, tid=str(t), trigger=c.get("m_IsTrigger", 0), enabled=c.get("m_Enabled", 1))
    if ty == "BoxCollider": e.update(size=V(c["m_Size"]), center=V(c["m_Center"]))
    elif ty == "CapsuleCollider": e.update(radius=c["m_Radius"], height=c["m_Height"], dir=c["m_Direction"], center=V(c["m_Center"]))
    elif ty == "SphereCollider": e.update(radius=c["m_Radius"], center=V(c["m_Center"]))
    else:
        mid = c["m_Mesh"]["m_PathID"]
        if mid not in O: e["nomesh"] = 1; coll[str(t)].append(e); continue
        m = O[mid].read(); b = m.m_LocalAABB
        e.update(center=[b.m_Center.x, b.m_Center.y, b.m_Center.z], size=[2 * b.m_Extent.x, 2 * b.m_Extent.y, 2 * b.m_Extent.z], mesh=m.m_Name)
    coll[str(t)].append(e)
def subtree(tid, acc):
    acc.append(str(tid))
    for c in T[tid]["ch"]:
        if c in partT or c not in T: continue
        subtree(c, acc)
out = dict(root=str(rootT), prefab=NAME, transforms=tr, parts=[], surfaces=[], engines=[], nozzles=[], hlds=[], responders=[], tanks=[], gears=[], airbrakes=[])
ap2t = {}
for k, m in aero.items():
    tid = go2t[m["_go"]]; ap2t[k] = str(tid); ln = m["liftNormal"]["m_PathID"]
    acc = []; subtree(tid, acc)
    comT = m.get("centerOfMass", {}).get("m_PathID", 0)
    out["parts"].append(dict(name=GO[m["_go"]]["name"], tid=str(tid), mass=m["mass"], wingArea=m["wingArea"], dragArea=m["dragArea"], airfoil=m["airfoil"], col=V(m["centerOfLift"]), liftNormal=str(ln) if ln in T else str(tid), channel=m["airflowChanneling"], anchor=str(m["connectedAnchor"]["m_PathID"]) if m.get("connectedAnchor", {}).get("m_PathID", 0) in T else None, streamlining=m.get("streamlining"), comT=str(comT) if comT in T else None, colliders=[c for t in acc for c in coll.get(t, [])]))
for k, m in cs.items():
    out["surfaces"].append(dict(name=GO[m["_go"]]["name"], tid=str(go2t[m["_go"]]), visible=str(go2t[m["visibleMesh"]["m_PathID"]]) if m["visibleMesh"]["m_PathID"] in go2t else None, attached=ap2t[m["attachedSurface"]["m_PathID"]], pitchRange=m["pitchRange"], rollRange=m["rollRange"], yawRange=m["yawRange"], servo=m["servoSpeed"], brakeRange=m["brakeRange"], maxSplit=m["maxSplit"], splitDrag=m.get("splitDrag"), yawSplitFactor=m.get("yawSplitFactor"), flap=m.get("flap")))
jn = {}
for k, m in noz.items():
    jn[k] = dict(tid=str(go2t[m["_go"]]), thrustT=str(m["thrustTransform"]["m_PathID"]), ab=[a["thrust"] for a in m["afterburners"]], abStart=[a["throttleStart"] for a in m["afterburners"]], part=ap2t.get(m["part"]["m_PathID"]))
    out["nozzles"].append(jn[k])
for k, m in eng.items():
    e = dict(name=GO[m["_go"]]["name"], tid=str(go2t[m["_go"]]), kind=("turbofan" if "staticThrust" in m else "turbojet"), vec=V(m["thrustVectoring"]), gain=V(m["thrustVectoringGain"]), vecMaxSpeed=m["thrustVectoringMaxAirspeed"], vecT=[str(x["m_PathID"]) for x in m["vectoringTransforms"]], nozzles=[jn[x["m_PathID"]]["tid"] for x in m["nozzles"] if x["m_PathID"] in jn],
             remap=[m["throttleRemap"]["x"], m["throttleRemap"]["y"]], minRPM=m["minRPM"], maxRPM=m["maxRPM"], spool=m["spoolRate"], alt=[[c["time"], c["value"], c["inSlope"], c["outSlope"]] for c in m["altitudeThrust"]["m_Curve"]])
    if "staticThrust" in m: e.update(staticThrust=m["staticThrust"], speedThrust=[[c["time"], c["value"]] for c in m["speedThrust"]["m_Curve"]])
    else: e.update(staticThrust=m["maxThrust"], maxSpeed=m["maxSpeed"], speedThrust=[[0, 1], [m["maxSpeed"], 1], [m["maxSpeed"] * 1.2, 0]])
    out["engines"].append(e)
for k, m in hld.items():
    out["hlds"].append(dict(tid=str(go2t[m["_go"]]), part=ap2t[m["aeroPart"]["m_PathID"]], speedDeployed=m["speedDeployed"], speedRetracted=m["speedRetracted"], deployedArea=m["deployedArea"], alphaMin=m["alphaMin"], alphaFactor=m["alphaFactor"], moving=[dict(tid=str(mp["transform"]["m_PathID"]), move=mp["move"], rotate=mp["rotate"], pR=V(mp["positionRetracted"]), pD=V(mp["positionDeployed"]), aR=V(mp["anglesRetracted"]), aD=V(mp["anglesDeployed"])) for mp in m["movingParts"]]))
for k, m in tank.items(): out["tanks"].append(dict(tid=str(go2t[m["_go"]]), capacity=m["fuelCapacity"]))
for k, m in gear.items(): out["gears"].append(str(go2t[m["_go"]]))
for k, m in abk.items(): out["airbrakes"].append(dict(tid=str(go2t[m["_go"]]), part=ap2t.get(m["part"]["m_PathID"]), dragAmount=m["dragAmount"], maxAngle=m["maxAngle"], openSpeed=m["openSpeed"], transforms=[str(x["m_PathID"]) for x in m["transforms"]]))
# airfoils from the Aircraft's own parameters asset
# the AircraftParameters asset is named after the prefab (Fighter1Parameters, Multirole1_parameters, ...)
p = None
for pid, o in O.items():
    if o.type.name != "MonoBehaviour": continue
    try: d = o.read_typetree()
    except Exception: continue
    if "airfoils" in d and d.get("m_Name", "").lower().replace("_", "").startswith(NAME.lower() + "parameters"):
        p = d; print("parameters asset", pid, d["m_Name"]); break
if p is None: raise SystemExit("no AircraftParameters asset for " + NAME)
def curve(c): return dict(keys=[[k["time"], k["value"], k["inSlope"], k["outSlope"], k.get("weightedMode", 0), k.get("inWeight", 0.333), k.get("outWeight", 0.333)] for k in c["m_Curve"]], pre=c["m_PreInfinity"], post=c["m_PostInfinity"])
out["airfoils"] = [dict(name=a["name"], lift=curve(a["liftCoef"]), drag=curve(a["dragCoef"])) for a in p["airfoils"]]
out["parameters"] = {k: v for k, v in p.items() if not isinstance(v, (dict, list))}
fbw = [m for m in MB.values() if "flyByWire" in m]
if fbw: out["stockFbw"] = {k: v for k, v in fbw[0]["flyByWire"].items() if not isinstance(v, dict)}
aoal = [m for m in MB.values() if "aoaLimiter" in m]
if aoal: out["aoaLimiter"] = aoal[0]["aoaLimiter"]
json.dump(out, open(OUT, "w"), indent=0)
print(len(tr), "parts", len(out["parts"]), "colliders", sum(len(q["colliders"]) for q in out["parts"]), "surfaces", len(out["surfaces"]), "engines", len(out["engines"]), [(e["name"], e["kind"], e["staticThrust"]) for e in out["engines"]], "tanks", len(out["tanks"]), sum(t["capacity"] for t in out["tanks"]), "hlds", len(out["hlds"]), "airbrakes", len(out["airbrakes"]))
print("parts:", [(q["name"], q["mass"]) for q in out["parts"]])
print("surfaces:", [(s["name"], s["pitchRange"], s["rollRange"], s["yawRange"], s["servo"]) for s in out["surfaces"]])
print("no colliders:", [q["name"] for q in out["parts"] if not q["colliders"]])
