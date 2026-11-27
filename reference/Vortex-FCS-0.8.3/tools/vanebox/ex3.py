import UnityPy, pickle, numpy as np, sys
import os as _os  # kit 5.0: NO_GAME = your Nuclear Option folder
D = _os.path.join(_os.environ.get("NO_GAME", "/mnt/user-data/uploads/Nuclear Option"), "NuclearOption_Data")
env=UnityPy.load(D+"/resources.assets")
RA=[f for n,f in env.files.items() if n.endswith("resources.assets")][0]
objs=RA.objects
info=pickle.load(open("/home/claude/work/geo/vortex_h.pkl","rb"))
meshes={}; mats={}
for r in info.values():
    for c in r["comps"]:
        if c.get("mesh") and c.get("meshfile",0)==0:
            meshes.setdefault(c["mesh"],[]).append((r["name"],c["type"]))
        for m in c.get("mats",[]):
            mats.setdefault(tuple(m),[]).append(r["name"])
out={}
for mid,users in sorted(meshes.items()):
    o=objs.get(mid)
    if o is None or o.type.name!="Mesh": print(mid,"missing/not mesh",users); continue
    d=o.read_typetree()
    sd=d.get("m_StreamData",{}); vd=d.get("m_VertexData",{}); ab=d["m_LocalAABB"]
    out[mid]=dict(name=d["m_Name"],users=users,vcount=vd.get("m_VertexCount"),stream=(sd.get("path"),sd.get("offset"),sd.get("size")),readable=d.get("m_IsReadable"),
             inl=len(vd.get("m_DataSize",b"")),c=(ab["m_Center"]["x"],ab["m_Center"]["y"],ab["m_Center"]["z"]),e=(ab["m_Extent"]["x"],ab["m_Extent"]["y"],ab["m_Extent"]["z"]),
             sub=d["m_SubMeshes"],idxfmt=d.get("m_IndexFormat"),idx=bytes(d.get("m_IndexBuffer",b"")),channels=vd.get("m_Channels"),vdata=bytes(vd.get("m_DataSize",b"")),
             bakedC=len(d.get("m_BakedConvexCollisionMesh",b"")),bakedT=len(d.get("m_BakedTriangleCollisionMesh",b"")))
pickle.dump(out,open("/home/claude/work/geo/meshes.pkl","wb"))
lo=min(r["stream"][1] for r in out.values() if r["stream"][2]); hi=max(r["stream"][1]+r["stream"][2] for r in out.values() if r["stream"][2])
print("mesh stream range",lo,hi,hi-lo, "sum",sum(r["stream"][2] for r in out.values()), "n",len(out), "inline", sum(1 for r in out.values() if r["inl"]))
for mid,r in out.items():
    if any(k in u[0] for u in r["users"] for k in ("liftFan","fanDoor","intake","SmallFighter1","fuselage_LOD","wingRoot")):
        print(mid,r["name"],"v",r["vcount"],"stream",r["stream"][1:],"rd",r["readable"],"inl",r["inl"],"c %.3f %.3f %.3f"%r["c"],"e %.3f %.3f %.3f"%r["e"],"sub",len(r["sub"]),"baked",r["bakedC"],r["bakedT"],[u[0]+":"+u[1][:6] for u in r["users"]])
# materials
mo={}
texs={}
for (fid,pid),users in mats.items():
    if fid!=0: print("ext material",fid,pid,users[:3]); continue
    o=objs.get(pid)
    if o is None: continue
    d=o.read_typetree()
    sh=d["m_Shader"]; sp=d["m_SavedProperties"]
    te={k:(v["m_Texture"]["m_FileID"],v["m_Texture"]["m_PathID"],v["m_Scale"],v["m_Offset"]) for k,v in sp["m_TexEnvs"]}
    mo[pid]=dict(name=d["m_Name"],shader=(sh["m_FileID"],sh["m_PathID"]),tex=te,floats=dict(sp["m_Floats"]),colors={k:(v["r"],v["g"],v["b"],v["a"]) for k,v in sp["m_Colors"]},kw=d.get("m_ValidKeywords"),users=users,queue=d.get("m_CustomRenderQueue"),raw=d)
    for k,(f,p,_,_) in te.items():
        if p: texs.setdefault((f,p),[]).append((d["m_Name"],k))
pickle.dump(mo,open("/home/claude/work/geo/mats.pkl","wb"))
for pid,m in mo.items():
    shn="?"
    if m["shader"][0]==0 and m["shader"][1] in objs:
        try: shn=objs[m["shader"][1]].read_typetree()["m_ParsedForm"]["m_Name"]
        except Exception as e: shn="err"
    print("MAT",pid,m["name"],"shader",m["shader"],shn,"users",len(m["users"]),m["users"][:6])
    print("    tex",{k:v[:2] for k,v in m["tex"].items() if v[1]}, "floats",{k:round(v,3) for k,v in m["floats"].items()}, "colors",{k:tuple(round(x,3) for x in v) for k,v in m["colors"].items()}, "kw",m["kw"], "q",m["queue"])
to={}
for (f,p),u in texs.items():
    if f!=0: print("ext tex",f,p,u); continue
    o=objs.get(p); d=o.read_typetree(); sd=d["m_StreamData"]
    to[p]=dict(name=d["m_Name"],w=d["m_Width"],h=d["m_Height"],fmt=d["m_TextureFormat"],mips=d["m_MipCount"],stream=(sd["path"],sd["offset"],sd["size"]),users=u,cs=d.get("m_ColorSpace"))
    print("TEX",p,to[p])
pickle.dump(to,open("/home/claude/work/geo/texs.pkl","wb"))
