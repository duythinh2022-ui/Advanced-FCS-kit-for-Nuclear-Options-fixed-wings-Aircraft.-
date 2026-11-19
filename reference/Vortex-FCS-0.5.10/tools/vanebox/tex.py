import numpy as np, struct, io
from PIL import Image
S="/mnt/user-data/uploads/NO mods/_vortex_slices/"
def dds(w,h,fourcc,data,mips=1):
    hdr=bytearray(128); hdr[0:4]=b'DDS '
    struct.pack_into('<7I',hdr,4,124,0x1|0x2|0x4|0x1000|0x80000,h,w,len(data),0,1)
    struct.pack_into('<2I4s5I',hdr,76,32,0x4,fourcc,0,0,0,0,0)
    struct.pack_into('<I',hdr,108,0x1000)
    return bytes(hdr)+data
def load(name,w,h,fmt):
    b=open(S+name+".bin","rb").read()
    blk=8 if fmt==10 else 16; n=max(1,w//4)*max(1,h//4)*blk
    im=Image.open(io.BytesIO(dds(w,h,b'DXT1' if fmt==10 else b'DXT5',b[:n]))); im.load()
    return im
T={'skin_b':(2048,2048,10),'skin_m':(2048,2048,12),'skin_n':(2048,2048,12),'skin_ao':(2048,2048,10),'mech_b':(2048,2048,12),'mech_m':(2048,2048,12),'mech_n':(2048,2048,12),'mech_ao':(2048,2048,10),'struct_b':(1024,512,10),'struct_m':(1024,512,12),'struct_n':(1024,512,12),'struct_ao':(1024,512,10)}
if __name__=="__main__":
    for k,(w,h,f) in T.items():
        im=load(k,w,h,f); print(k,im.mode,im.size, np.asarray(im).reshape(-1,len(im.getbands())).mean(0).round(1))
        im.save(f"/home/claude/work/geo/tex_{k}.png")
