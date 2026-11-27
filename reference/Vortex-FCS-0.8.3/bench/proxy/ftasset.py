import struct,sys,json
b=open(sys.argv[1],'rb').read(); o=0
def i32():
    global o; v=struct.unpack_from('<i',b,o)[0]; o+=4; return v
def f32():
    global o; v=struct.unpack_from('<f',b,o)[0]; o+=4; return v
def txt():
    global o; n=i32(); s=b[o:o+n].decode(); o+=n; return s
def pt(): return [f32(),f32(),f32()]
def mesh(nm,visual):
    global o
    nv=i32(); nf=i32()
    o+= nv*(12+(12+8 if visual else 0)); o+= nf*((4 if visual else 0)+12)
assert i32()==0x46535431; ver=i32(); src=txt()
pieces=[]
for _ in range(i32()):
    p=dict(Name=txt(),Path=txt(),Parent=txt(),Owner=txt(),Motion=txt(),Replace=i32()==1)
    nm=i32(); p['Materials']=[txt() for _ in range(nm)]
    for j in range(3): mesh(nm,True)
    for j in range(i32()): mesh(1,False)
    pieces.append(p)
areas=[dict(Path=txt(),Before=f32(),After=f32(),BeforeCenter=pt(),AfterCenter=pt(),HighLiftRatio=f32()) for _ in range(i32())]
hinges=[]
for _ in range(i32()):
    hinges.append(dict(Tag=txt(),Path=txt(),Pivot=pt(),Rot=[f32() for _ in range(4)],Axis=pt(),Angle=f32(),Stroke=pt()))
regions=[]
for _ in range(i32() if ver==3 else 0):
    r=dict(Id=txt(),Attachment=txt(),Budget=txt(),Area=f32(),Center=pt(),Anchor=pt())
    r['States']=[dict(Piece=txt(),AreaFraction=f32(),DragScale=f32()) for _ in range(3)]
    regions.append(r)
assert o==len(b),(o,len(b))
json.dump(dict(version=ver,areas=areas,regions=regions,hinges=hinges,pieces=[{k:v for k,v in p.items() if k!='Materials'} for p in pieces]),open(sys.argv[2],'w'),indent=1)
print('version',ver,'areas',len(areas),'regions',len(regions))
for a in areas: print(f"{a['Path']:30s} {a['Before']:.3f} -> {a['After']:.3f} (x{a['After']/a['Before']:.2f}) center {['%.2f'%v for v in a['BeforeCenter']]} -> {['%.2f'%v for v in a['AfterCenter']]} HL {a['HighLiftRatio']:.2f}")
for r in regions: print(r['Id'],r['Attachment'],r['Budget'],round(r['Area'],3),[round(v,2) for v in r['Center']])
