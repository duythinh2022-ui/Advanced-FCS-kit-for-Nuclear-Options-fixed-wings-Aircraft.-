import UnityPy, json, numpy as np
from scipy.spatial import ConvexHull
env=UnityPy.load("bundle.bin")
objs={o.path_id:o for o in env.objects}
d=json.load(open("bench_airframe.json"))
T={t["id"]:t for t in d["transforms"]}
# map transform id -> meshcollider mesh path ids
go2t={}
for o in env.objects:
    if o.type.name=="Transform":
        tt=o.read_typetree(); go2t[tt["m_GameObject"]["m_PathID"]]=str(o.path_id)
def mass_props(P):
    # solid convex hull: volume, centroid, inertia (unit density) via tetrahedra from centroid
    h=ConvexHull(P); c0=P[h.vertices].mean(0)
    V=0; C=np.zeros(3); I=np.zeros((3,3))
    for s in h.simplices:
        a,b,c=P[s]-c0
        v=abs(np.dot(a,np.cross(b,c)))/6
        if v<=0: continue
        cen=(a+b+c)/4
        # covariance of tetra (with vertex at origin)
        Cm=np.zeros((3,3))
        for x in (a,b,c): Cm+=np.outer(x,x)
        s3=a+b+c; Cm=(Cm+np.outer(s3,s3))*v/20
        V+=v; C+=v*cen; I+=Cm
    C/=V
    # covariance about centroid
    Cc=I-V*np.outer(C,C)
    It=np.trace(Cc)*np.eye(3)-Cc
    return V, C+c0, It
out={}
for o in env.objects:
    if o.type.name!="MeshCollider": continue
    dd=o.read_typetree(); tid=go2t.get(dd["m_GameObject"]["m_PathID"])
    if tid not in T: continue
    m=objs[dd["m_Mesh"]["m_PathID"]].read()
    try:
        verts=np.array(m.m_Vertices if hasattr(m,"m_Vertices") and m.m_Vertices else [], dtype=float)
    except Exception as e:
        verts=np.array([])
    if verts.size==0:
        try:
            v=m.export().splitlines(); verts=np.array([[float(x) for x in l.split()[1:4]] for l in v if l.startswith("v ")])
            verts[:,0]*=-1  # obj export flips x
        except Exception as e:
            print("fail",tid,e); continue
    verts=verts.reshape(-1,3)
    # apply lossy scale of transform chain
    s=np.ones(3); t=tid
    while t is not None:
        s*=np.array(T[t]["ls"]); t=T[t]["parent"]
    P=verts*s
    try:
        V,C,I=mass_props(P)
    except Exception as e:
        print("hull fail",tid,e); continue
    out.setdefault(tid,[]).append(dict(V=V,C=C.tolist(),I=(I).tolist(),n=len(P),bmin=P.min(0).tolist(),bmax=P.max(0).tolist()))
json.dump(out,open("hulls.json","w"))
print(len(out), sum(len(v) for v in out.values()))
for k,v in list(out.items())[:5]: print(T[k]["name"], [(round(x["V"],3), np.round(x["C"],2).tolist(), np.round((np.array(x["bmin"])+np.array(x["bmax"]))/2,2).tolist()) for x in v])
