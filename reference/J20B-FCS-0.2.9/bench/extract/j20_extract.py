#!/usr/bin/env python3
# J-20B 0.1.0: bench airframe from the J-20B Weilong mod's bundle (J-20B Weilong_0.0.16.nobp) + its AeroControl runtime contract.
#   j20_extract.py <bundle.nobp> <RuntimeContract.json> <out.json>
# Same schema as calib/stock_extract.py (transforms, parts with colliders, engines, nozzles, tanks, gears, airfoils, parameters) plus
# convex-hull mass properties for every MeshCollider whose mesh is in the bundle, and a "j20" block: the mod's hinge surfaces
# (role, owner part, hinge transform, rest rotation, travel, servo, stick gains and schedules), force-point markers, camber normals
# and the LE-flap high-lift area spec. The game's own collider meshes (engines, nozzles) are not in the bundle: those keep the AABB box.
import UnityPy, json, sys, collections
import numpy as np
from scipy.spatial import ConvexHull
B, CTR, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
NAME = "J20NativeMinimum"
data = open(B, 'rb').read(); i0 = data.find(b'UnityFS'); env = UnityPy.load(data[i0:])
O = {o.path_id: o for o in env.objects}
T = {}; GO = {}
for o in env.objects:
    if o.type.name == "Transform":
        d = o.read_typetree(); T[o.path_id] = dict(go=d["m_GameObject"]["m_PathID"], parent=d["m_Father"]["m_PathID"], p=d["m_LocalPosition"], q=d["m_LocalRotation"], s=d["m_LocalScale"], ch=[c["m_PathID"] for c in d["m_Children"]])
    elif o.type.name == "GameObject":
        d = o.read_typetree(); GO[o.path_id] = dict(name=d["m_Name"], comps=[c["component"]["m_PathID"] for c in d["m_Component"]])
go2t = {t["go"]: k for k, t in T.items()}
roots = [tid for tid, t in T.items() if t["parent"] == 0 and GO[t["go"]]["name"] == NAME]
rootT = roots[0]
def sub(tid, acc):
    acc.append(tid)
    for c in T[tid]["ch"]:
        if c in T: sub(c, acc)
    return acc
keep = sub(rootT, []); keepS = set(keep)
gos = set(T[t]["go"] for t in keep)
MB = {}; OTH = {}
for gid in gos:
    for cid in GO[gid]["comps"]:
        o = O.get(cid)
        if o is None: continue
        if o.type.name == "MonoBehaviour":
            try:
                d = o.read_typetree(); d["_cls"] = o.read().m_Script.read().m_ClassName
            except Exception: continue
            d["_go"] = gid; d["_id"] = cid; MB[cid] = d
        elif o.type.name not in ("Transform", "MeshRenderer", "MeshFilter", "LODGroup", "SkinnedMeshRenderer"):
            try:
                d = o.read_typetree(); d["_type"] = o.type.name; d["_go"] = gid; OTH[cid] = d
            except Exception: pass
print("transforms", len(keep), "MBs", collections.Counter(m["_cls"] for m in MB.values()).most_common(12))
def V(d, keys="xyz"): return [d[k] for k in keys]
tr = [dict(id=str(t), name=GO[T[t]["go"]]["name"], parent=str(T[t]["parent"]) if T[t]["parent"] in keepS else None, lp=V(T[t]["p"]), lr=V(T[t]["q"], "xyzw"), ls=V(T[t]["s"])) for t in keep]
def lossy_scale(tid):
    s = np.ones(3)
    while tid in T:
        s *= np.array(V(T[tid]["s"])); tid = T[tid]["parent"]
    return s
def hull_props(verts):
    h = ConvexHull(verts); c0 = verts[h.vertices].mean(0)
    V0 = 0.0; C = np.zeros(3); I = np.zeros((3, 3))
    tets = []
    for s in h.simplices:
        a, b, c = verts[s] - c0
        v = abs(np.dot(a, np.cross(b, c))) / 6.0
        if v <= 0: continue
        tets.append((a, b, c, v)); V0 += v; C += v * (a + b + c) / 4.0
    C /= V0
    for a, b, c, v in tets:
        # unit-density inertia of a tetrahedron (0, a, b, c) about the hull centroid: covariance form
        P = np.array([np.zeros(3), a, b, c]) - C
        cov = (v / 20.0) * (P.sum(0)[:, None] * P.sum(0)[None, :] + sum(np.outer(p, p) for p in P))
        I += np.trace(cov) * np.eye(3) - cov
    return float(V0), (C + c0).tolist(), I.tolist()
aero = {k: m for k, m in MB.items() if m["_cls"] == "AeroPart"}
eng = {k: m for k, m in MB.items() if m["_cls"] in ("Turbojet", "Turbofan")}
noz = {k: m for k, m in MB.items() if m["_cls"] == "JetNozzle"}
tank = {k: m for k, m in MB.items() if m["_cls"] == "FuelTank"}
gear = {k: m for k, m in MB.items() if m["_cls"] == "LandingGear"}
partT = {go2t[m["_go"]] for m in aero.values()}
coll = collections.defaultdict(list); nh = 0
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
        if c["m_Mesh"]["m_FileID"] != 0 or mid not in O: e["nomesh"] = 1; coll[str(t)].append(e); continue
        m = O[mid].read(); b = m.m_LocalAABB
        e.update(center=[b.m_Center.x, b.m_Center.y, b.m_Center.z], size=[2 * b.m_Extent.x, 2 * b.m_Extent.y, 2 * b.m_Extent.z], mesh=m.m_Name)
        try:
            vs = np.array([[l2 for l2 in map(float, l.split()[1:4])] for l in m.export().split('\n') if l.startswith('v ')]); vs[:, 0] *= -1
            vs = vs * lossy_scale(t)
            Vh, Ch, Ih = hull_props(vs); e["hull"] = dict(V=Vh, C=Ch, I=Ih); nh += 1
        except Exception as ex: print("hull fail", m.m_Name, ex)
    coll[str(t)].append(e)
def subtree(tid, acc):
    acc.append(str(tid))
    for c in T[tid]["ch"]:
        if c in partT or c not in T: continue
        subtree(c, acc)
out = dict(root=str(rootT), prefab=NAME, transforms=tr, parts=[], surfaces=[], engines=[], nozzles=[], hlds=[], responders=[], tanks=[], gears=[], airbrakes=[])
ap2t = {}
def path_of(tid):
    n = []
    while tid in T and T[tid]["parent"] in T:
        n.append(GO[T[tid]["go"]]["name"]); tid = T[tid]["parent"]
    return '/'.join(reversed(n))
for k, m in aero.items():
    tid = go2t[m["_go"]]; ap2t[k] = str(tid); ln = m["liftNormal"]["m_PathID"]
    acc = []; subtree(tid, acc)
    comT = m.get("centerOfMass", {}).get("m_PathID", 0)
    out["parts"].append(dict(name=GO[m["_go"]]["name"], path=path_of(tid), tid=str(tid), mass=m["mass"], wingArea=m["wingArea"], dragArea=m["dragArea"], airfoil=m["airfoil"], col=V(m["centerOfLift"]), liftNormal=str(ln) if ln in T else str(tid), channel=m["airflowChanneling"], anchor=None, streamlining=m.get("streamlining"), comT=str(comT) if comT in T else None, colliders=[c for t in acc for c in coll.get(t, [])]))
jn = {}
for k, m in noz.items():
    jn[k] = dict(tid=str(go2t[m["_go"]]), thrustT=str(m["thrustTransform"]["m_PathID"]), ab=[a["thrust"] for a in m["afterburners"]], abStart=[a["throttleStart"] for a in m["afterburners"]], part=ap2t.get(m.get("part", {}).get("m_PathID")))
    out["nozzles"].append(jn[k])
for k, m in eng.items():
    e = dict(name=GO[m["_go"]]["name"], tid=str(go2t[m["_go"]]), kind="turbojet", vec=V(m["thrustVectoring"]), gain=V(m["thrustVectoringGain"]), vecMaxSpeed=m["thrustVectoringMaxAirspeed"], vecT=[str(x["m_PathID"]) for x in m["vectoringTransforms"]], nozzles=[jn[x["m_PathID"]]["tid"] for x in m["nozzles"] if x["m_PathID"] in jn],
             remap=[m["throttleRemap"]["x"], m["throttleRemap"]["y"]], minRPM=m["minRPM"], maxRPM=m["maxRPM"], spool=m["spoolRate"], alt=[[c["time"], c["value"], c["inSlope"], c["outSlope"]] for c in m["altitudeThrust"]["m_Curve"]])
    e.update(staticThrust=m["maxThrust"], maxSpeed=m["maxSpeed"], speedThrust=[[0, 1], [m["maxSpeed"], 1], [m["maxSpeed"] * 1.2, 0]])
    out["engines"].append(e)
for k, m in tank.items(): out["tanks"].append(dict(tid=ap2t.get(m["part"]["m_PathID"], str(go2t[m["_go"]])), capacity=m["fuelCapacity"]))
for k, m in gear.items(): out["gears"].append(str(go2t[m["_go"]]))
out["gearAxles"] = [str(m["axle"]["m_PathID"]) for m in gear.values() if m.get("axle", {}).get("m_PathID", 0) in T]
P = None
ALLMB = []
for o in env.objects:
    if o.type.name != "MonoBehaviour": continue
    try: d = o.read_typetree()
    except Exception: continue
    ALLMB.append(d)
    if P is None and "airfoils" in d and "aircraftName" in d: P = d
def curve(c): return dict(keys=[[k["time"], k["value"], k["inSlope"], k["outSlope"], k.get("weightedMode", 0), k.get("inWeight", 0.333), k.get("outWeight", 0.333)] for k in c["m_Curve"]], pre=c["m_PreInfinity"], post=c["m_PostInfinity"])
out["airfoils"] = [dict(name=a["name"], lift=curve(a["liftCoef"]), drag=curve(a["dragCoef"])) for a in P["airfoils"]]
out["parameters"] = {k: v for k, v in P.items() if not isinstance(v, (dict, list)) and not k.startswith("_")}
fbw = [m for m in ALLMB if "flyByWire" in m]
if fbw: out["stockFbw"] = {k: v for k, v in fbw[0]["flyByWire"].items() if not isinstance(v, dict)}
aoal = [m for m in ALLMB if "aoaLimiter" in m]
if aoal: out["aoaLimiter"] = aoal[0]["aoaLimiter"]
# ---- the J-20B mod's runtime contract
C = json.load(open(CTR))
byPath = {path_of(t): str(t) for t in keep}
partByPath = {q["path"]: q["tid"] for q in out["parts"]}
partByPath[""] = str(rootT)
def tidOf(p): return byPath.get(p)
j = dict(surfaces=[], forcePoints=[], camber=[], highLift=[])
for s in C["surfaces"]:
    own = s["ownerLogicalPath"]; ownT = partByPath[own]
    hp = (own + "/" if own else "") + s["hingeRelativePath"]
    j["surfaces"].append(dict(id=s["id"], role=s["role"], side=s["side"], owner=ownT, hinge=tidOf(hp), rest=s["neutralLocalRotation"], min=s["minDegrees"], max=s["maxDegrees"], servo=s["servoDegreesPerSecond"],
        pitchGain=s["pitchGain"], rollGain=s["rollGain"], yawGain=s["yawGain"], base=s["baseDegrees"], lowSpeedDegrees=s["lowSpeedDegrees"], lowSpeedFull=s["lowSpeedFull"], lowSpeedZero=s["lowSpeedZero"], lowSpeedRequiresGear=s["lowSpeedRequiresGear"],
        aoaGain=s["aoaGain"], aoaDeadzone=s["aoaDeadzone"], aoaMaxDegrees=s["aoaMaxDegrees"], aoaSpeedFull=s["aoaSpeedFull"], aoaSpeedZero=s["aoaSpeedZero"]))
for f in C["forcePoints"]:
    own = f["ownerLogicalPath"]; mp = (own + "/" if own else "") + f["markerRelativePath"]
    j["forcePoints"].append(dict(owner=partByPath[own], marker=tidOf(mp), copRoot=f["copRoot"]))
for c in C["wingCamber"]:
    own = c["ownerLogicalPath"]
    j["camber"].append(dict(owner=partByPath[own], normal=tidOf(own + "/" + c["liftNormalRelativePath"]), rest=c["neutralLocalRotation"]))
for h in C["runtimeHighLift"]:
    j["highLift"].append(dict(owner=partByPath[h["ownerLogicalPath"]], baseArea=h["baseArea"], additionalArea=h["additionalArea"], leadingAdditionalArea=h["leadingAdditionalArea"], leadingFullDegrees=h["leadingFullDegrees"], leadingSign=h["leadingDeploymentSign"], leadingIds=h["leadingSourceSurfaceIds"]))
out["j20"] = j
json.dump(out, open(OUT, "w"))
print("parts", len(out["parts"]), "hulls", nh, "colliders", sum(len(q["colliders"]) for q in out["parts"]), "engines", [(e["name"], e["staticThrust"]) for e in out["engines"]], "tanks", sum(t["capacity"] for t in out["tanks"]), "gears", len(out["gears"]))
print("j20 surfaces", [(s["id"], s["role"], bool(s["hinge"])) for s in j["surfaces"]])
print("force points missing markers:", [f["owner"] for f in j["forcePoints"] if not f["marker"]], "camber missing:", [c["owner"] for c in j["camber"] if not c["normal"]])
