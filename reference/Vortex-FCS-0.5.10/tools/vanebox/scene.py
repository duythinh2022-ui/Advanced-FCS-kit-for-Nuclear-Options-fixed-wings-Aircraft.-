import pickle, numpy as np
G="/home/claude/work/geo/"
meshes=pickle.load(open(G+"meshes.pkl","rb")); info=pickle.load(open(G+"vortex_h.pkl","rb")); D=pickle.load(open(G+"dec.pkl","rb")); mats=pickle.load(open(G+"mats.pkl","rb"))
byname={}
for r in info.values(): byname.setdefault(r["name"],r)
def node(name): return byname[name]
def M(name): return np.array(byname[name]["M"])
def mesh_of(name):
    r=byname[name]
    mf=[c for c in r["comps"] if c["type"]=="MeshFilter"]
    mr=[c for c in r["comps"] if c["type"]=="MeshRenderer"]
    if not mf or mf[0]["mesh"] not in D: return None
    d=D[mf[0]["mesh"]]
    mn=[mats[p]["name"] if f==0 and p in mats else "?" for f,p in mr[0]["mats"]] if mr else []
    return d,mn
def world(name, Mover=None):
    """returns P (root frame), N, UV, subs, matnames"""
    d,mn=mesh_of(name); Mw=M(name) if Mover is None else Mover
    P=d["P"]@Mw[:3,:3].T+Mw[:3,3]; N=d["N"]@np.linalg.inv(Mw[:3,:3])
    N=N/np.maximum(1e-9,np.linalg.norm(N,axis=1,keepdims=True))
    return P,N,d["UV"],d["subs"],mn
def rot_about(axis_point, axis_dir, deg):
    a=np.radians(deg); k=np.array(axis_dir,float); k/=np.linalg.norm(k)
    K=np.array([[0,-k[2],k[1]],[k[2],0,-k[0]],[-k[1],k[0],0]])
    R=np.eye(3)+np.sin(a)*K+(1-np.cos(a))*K@K
    T=np.eye(4); T[:3,:3]=R; T[:3,3]=np.array(axis_point)-R@np.array(axis_point); return T
def euler_unity(x,y,z):
    # Unity: R = Ry * Rx * Rz (left-handed, but same matrix forms)
    cx,sx=np.cos(np.radians(x)),np.sin(np.radians(x)); cy,sy=np.cos(np.radians(y)),np.sin(np.radians(y)); cz,sz=np.cos(np.radians(z)),np.sin(np.radians(z))
    Rx=np.array([[1,0,0],[0,cx,-sx],[0,sx,cx]]); Ry=np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]]); Rz=np.array([[cz,-sz,0],[sz,cz,0],[0,0,1]])
    return Ry@Rx@Rz
def plane_cut(P,tris,axis,val):
    """segments of the mesh cut by plane coord[axis]=val; returns (n,2,3)"""
    d=P[:,axis]-val; t=tris; da=d[t]
    segs=[]
    s=np.sign(da); mixed=(s.min(1)<0)&(s.max(1)>0)
    for tri,dd in zip(t[mixed],da[mixed]):
        pts=[]
        for i in range(3):
            a,b=tri[i],tri[(i+1)%3]; fa,fb=d[a],d[b]
            if (fa<0)!=(fb<0): u=fa/(fa-fb); pts.append(P[a]+u*(P[b]-P[a]))
        if len(pts)==2: segs.append(pts)
    return np.array(segs) if segs else np.zeros((0,2,3))
