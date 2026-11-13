# Bench calibration (31 Oct 2026): convex-hull mass properties for every MeshCollider, as PhysX computes a convex
# MeshCollider's volume, centroid and inertia (uniform density over the hull). extract2.py approximated each mesh
# collider by its AABB box, which puts a swept wing panel's centroid at the box centre instead of the hull centroid
# (aft and inboard of it) and overstates its inertia. Run after extract2.py; it adds a "hull" entry
# {V, C, I} (local collider frame, unit density) to each MeshCollider in bench_airframe.json, which Airframe.BuildBodies
# already prefers over the box when present (UseHulls).
import json, sys, numpy as np, UnityPy
from scipy.spatial import ConvexHull

path = sys.argv[1] if len(sys.argv) > 1 else "bench_airframe.json"
env = UnityPy.load("bundle.bin")
objs = {o.path_id: o for o in env.objects}
# collider path-id -> (transform id, mesh path id)
T = {}; coll = {}
for o in env.objects:
    if o.type.name in ("Transform", "RectTransform"):
        d = o.read_typetree(); T[d["m_GameObject"]["m_PathID"]] = o.path_id
for o in env.objects:
    if o.type.name == "MeshCollider":
        d = o.read_typetree()
        coll.setdefault(str(T[d["m_GameObject"]["m_PathID"]]), []).append((d["m_Mesh"]["m_PathID"], d.get("m_Convex", True)))

def tet_props(verts, faces):
    # volume, centroid and inertia (about origin, unit density) of a closed triangle surface, by signed tetrahedra
    V = 0.0; C = np.zeros(3); I = np.zeros((3, 3))
    for f in faces:
        a, b, c = verts[f[0]], verts[f[1]], verts[f[2]]
        v = np.dot(a, np.cross(b, c)) / 6.0
        V += v; C += v * (a + b + c) / 4.0
        # inertia of a tetrahedron (origin, a, b, c) about the origin, unit density (covariance form)
        P = np.array([a, b, c])
        cov = v / 20.0 * (P.T @ P + np.outer(P.sum(0), P.sum(0)))   # second moments
        I += np.trace(cov) * np.eye(3) - cov
    if V <= 0: V = -V; C = -C; I = -I
    return V, C / V, I

def mesh_verts(mid):
    m = objs[mid].read()
    from UnityPy.helpers import MeshHelper
    mh = MeshHelper.MeshHandler(m); mh.process()
    return np.array(mh.m_Vertices, dtype=float).reshape(-1, 3)

j = json.load(open(path))
n = 0; miss = 0
for p in j["parts"]:
    for c in p["colliders"]:
        if c["type"] != "MeshCollider": continue
        lst = coll.get(c["tid"])
        if not lst: miss += 1; continue
        # several mesh colliders on one transform: take them in order
        idx = sum(1 for x in p["colliders"] if x["type"] == "MeshCollider" and x["tid"] == c["tid"] and x is not c and "hull" in x)
        mid, convex = lst[min(idx, len(lst) - 1)]
        verts = mesh_verts(mid)
        try:
            h = ConvexHull(verts)
        except Exception as e:
            miss += 1; continue
        # orient faces outward
        faces = []
        cen = verts[h.vertices].mean(0)
        for s, eq in zip(h.simplices, h.equations):
            a, b, c3 = verts[s]
            nrm = np.cross(b - a, c3 - a)
            if np.dot(nrm, eq[:3]) < 0: s = s[[0, 2, 1]]
            faces.append(s)
        V, C, I = tet_props(verts, faces)
        # inertia about the centroid
        Ic = I - V * (np.dot(C, C) * np.eye(3) - np.outer(C, C))
        c["hull"] = {"V": float(V), "C": [float(x) for x in C], "I": [[float(x) for x in row] for row in Ic], "nv": int(len(verts))}
        n += 1
json.dump(j, open(path, "w"))
print(f"hulls added: {n}, missing: {miss}")
# summary: box centre vs hull centroid per part (local collider frame)
for p in j["parts"]:
    for c in p["colliders"]:
        if "hull" in c:
            bc = np.array(c["center"]); hc = np.array(c["hull"]["C"]); bv = float(np.prod(c["size"]))
            print(f"{p['name'].replace('Aryx_KingRaptor','')[:18]:18} boxV {bv:7.2f} hullV {c['hull']['V']:7.2f}  box C {bc.round(2)}  hull C {hc.round(2)}  d {(hc-bc).round(2)}")
