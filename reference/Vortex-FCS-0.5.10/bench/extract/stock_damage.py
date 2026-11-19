#!/usr/bin/env python3
# Stock-jet part damage data from the game's resources.assets (bench damage model, 2 Nov 2026).
#   stock_damage.py <prefab name, e.g. Fighter1 | Multirole1> <out.json> [NuclearOption_Data dir]
# Same output as extract_damage.py (the Aryx-bundle version): per part hp, structural / integrity thresholds, joints
# (connected part, listed breakForce / breakTorque, anchor transform id), and each engine's damageThreshold + critical parts.
# Loading as calib/stock_extract.py: the stock assets carry no type trees, so they are generated from the Managed DLLs.
import UnityPy, json, sys, warnings
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
NAME, OUT = sys.argv[1], sys.argv[2]
D = sys.argv[3] if len(sys.argv) > 3 else "/mnt/user-data/uploads/Nuclear Option/NuclearOption_Data"
env = UnityPy.load(D + "/resources.assets", D + "/globalgamemanagers.assets", D + "/globalgamemanagers")
RA = [f for n, f in env.files.items() if n.endswith("resources.assets")][0]
g = TypeTreeGenerator(RA.unity_version); g.load_local_dll_folder(D + "/Managed"); env.typetree_generator = g
O = RA.objects
T = {}; GO = {}
for o in O.values():
    if o.type.name == "Transform":
        d = o.read_typetree(); T[o.path_id] = dict(go=d["m_GameObject"]["m_PathID"], parent=d["m_Father"]["m_PathID"], ch=[c["m_PathID"] for c in d["m_Children"]])
    elif o.type.name == "GameObject":
        d = o.read_typetree(); GO[o.path_id] = dict(name=d["m_Name"], comps=[c["component"]["m_PathID"] for c in d["m_Component"]])
go2t = {t["go"]: k for k, t in T.items()}
scripts = {}
for o in env.objects:
    if o.type.name == "MonoScript":
        try: scripts[o.path_id] = o.read_typetree()["m_ClassName"]
        except Exception: pass
roots = [(t["go"], tid) for tid, t in T.items() if t["parent"] == 0 and GO[t["go"]]["name"] == NAME]
if not roots: sys.exit("prefab not found: " + NAME)
rootGo, rootT = roots[0]
keep = []; st = [rootT]
while st:
    t = st.pop(); keep.append(t); st.extend(c for c in T[t]["ch"] if c in T)
MB = {}
for gid in set(T[t]["go"] for t in keep):
    for cid in GO[gid]["comps"]:
        o = O.get(cid)
        if o is None or o.type.name != "MonoBehaviour": continue
        try: d = o.read_typetree()
        except Exception: continue
        d["_cls"] = scripts.get(d["m_Script"]["m_PathID"], "?"); d["_go"] = gid; MB[cid] = d
name = {cid: GO[m["_go"]]["name"] for cid, m in MB.items()}
parts, engines = {}, {}
for cid, d in MB.items():
    if d["_cls"] in ("AeroPart", "UnitPart") or ("wingArea" in d and "joints" in d):
        js = []
        for j in d.get("joints") or []:
            anc = (j.get("anchor") or {}).get("m_PathID", 0)
            js.append(dict(to=name.get(j["connectedPart"]["m_PathID"]), breakForce=j["breakForce"], breakTorque=j["breakTorque"], anchorTid=str(anc) if anc else None))
        a = d.get("armorProperties") or {}
        parts[name[cid]] = dict(cls=d["_cls"], tid=str(go2t[d["_go"]]), hp=d.get("hitPoints", 100.0), structural=d.get("structuralThreshold", 0.0),
                                integrity=max(d.get("integrityThreshold", 0.0), -1e30), critical=int(d.get("criticalPart", 0)), joints=js,
                                armor=dict(pierceArmor=a.get("pierceArmor"), pierceTolerance=a.get("pierceTolerance"), blastTolerance=a.get("blastTolerance")))
    elif d["_cls"] in ("Turbofan", "Turbojet") or "damageThreshold" in d:
        crit = d.get("criticalParts", [])
        if crit and "m_PathID" in crit[0]:   # Turbojet: plain UnitPart references; condition from the hit part's hp vs damageThreshold
            engines[name[cid]] = dict(model="turbojet", damageThreshold=d.get("damageThreshold"), hp=100.0,
                                      critical=[dict(part=name.get(c["m_PathID"]), threshold=d.get("damageThreshold"), weight=1.0) for c in crit])
        else:                                 # Turbofan: own hp, critical parts with threshold and weight
            engines[name[cid]] = dict(model="turbofan", damageThreshold=d.get("damageThreshold"), hp=d.get("hitPoints", 100.0),
                                      critical=[dict(part=name.get(c["part"]["m_PathID"]), threshold=c["threshold"], weight=c["weight"]) for c in crit])
json.dump(dict(source=f"resources.assets:{NAME}", parts=parts, engines=engines), open(OUT, "w"), indent=1)
print(NAME, len(parts), "parts,", len(engines), "engines ->", OUT, "| classes", sorted(set(m["_cls"] for m in MB.values()))[:30])
