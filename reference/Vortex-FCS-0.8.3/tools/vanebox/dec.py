import pickle, numpy as np, struct
meshes=pickle.load(open("/home/claude/work/geo/meshes.pkl","rb")); info=pickle.load(open("/home/claude/work/geo/vortex_h.pkl","rb"))
B=open("/mnt/user-data/uploads/NO mods/_vortex_slices/mesh.bin","rb").read(); BASE=951047280
FMT={0:('f4',4),1:('f2',2),2:('u1',1),3:('i1',1),4:('u2',2),5:('i2',2),6:('u2',2),7:('i2',2),8:('u1',1),9:('i1',1),10:('u4',4),11:('i4',4)}
def decode(m):
    ch=m["channels"]; n=m["vcount"]
    # stream layout
    streams={}
    for i,c in enumerate(ch):
        if c["dimension"]==0: continue
        dim=c["dimension"]&0xF; dt,sz=FMT[c["format"]]
        streams.setdefault(c["stream"],[]).append((i,c["offset"],dt,dim,sz))
    off=m["stream"][1]-BASE; out={}
    pos=0
    for s in sorted(streams):
        items=streams[s]; stride=max(o+dim*sz for _,o,_,dim,sz in items); stride=(stride+3)//4*4
        blk=np.frombuffer(B,dtype=np.uint8,count=n*stride,offset=off+pos).reshape(n,stride)
        for i,o,dt,dim,sz in items:
            a=np.frombuffer(blk[:,o:o+dim*sz].tobytes(),dtype='<'+dt).reshape(n,dim)
            if dt in('u1',) and i not in(0,1,2): a=a/255.0
            out[i]=a.astype(np.float64)
        pos+=n*stride; pos=(pos+15)//16*16
    idx=np.frombuffer(m["idx"],dtype='<u2' if m["idxfmt"]==0 else '<u4')
    subs=[]
    for s in m["sub"]:
        st=s["firstByte"]//(2 if m["idxfmt"]==0 else 4); tri=idx[st:st+s["indexCount"]].reshape(-1,3).astype(np.int64)+s.get("baseVertex",0)
        subs.append(tri)
    return out,subs
D={}
bad=0
for mid,m in meshes.items():
    if not m["stream"][2]: continue
    try:
        ch,subs=decode(m)
        P=ch[0]; 
        c=np.array(m["c"]); e=np.array(m["e"])
        ok=np.allclose(P.min(0),c-e,atol=2e-3) and np.allclose(P.max(0),c+e,atol=2e-3)
        if not ok: bad+=1; print("AABB mismatch",mid,m["name"],P.min(0),c-e,P.max(0),c+e)
        D[mid]=dict(name=m["name"],P=P,N=ch.get(1),T=ch.get(2),C=ch.get(3),UV=ch.get(4),UV1=ch.get(5),subs=subs)
    except Exception as ex:
        bad+=1; print("fail",mid,m["name"],ex, m["channels"][:6])
print("decoded",len(D),"bad",bad)
pickle.dump(D,open("/home/claude/work/geo/dec.pkl","wb"))
m=meshes[3939]; print([ (i,c) for i,c in enumerate(m["channels"]) if c["dimension"]])
d=D[3939]; print({k:(v.shape if hasattr(v,'shape') else None) for k,v in d.items() if k not in('name','subs')}, [len(s) for s in d["subs"]])
print("uv range",d["UV"].min(0),d["UV"].max(0), "color sample", None if d["C"] is None else d["C"][:3])
