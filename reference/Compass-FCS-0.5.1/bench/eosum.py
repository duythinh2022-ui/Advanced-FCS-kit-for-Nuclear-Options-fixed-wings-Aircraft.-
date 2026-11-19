import sys,glob,os,pandas as pd
tags=sys.argv[1:]
names=sorted(set(os.path.basename(f)[:-4] for t in tags for f in glob.glob(f'runs/eo_{t}/*.csv')))
for n in names:
    out=[]
    for t in tags:
        f=f'runs/eo_{t}/{n}.csv'
        if not os.path.exists(f): out.append(f'{t}: -'); continue
        d=pd.read_csv(f)
        if len(d)<10: out.append(f'{t}: FAIL'); continue
        late=d[d.t>4]
        cross=((d.ailL*d.flapL<0)&(d.ailL.abs()>15)&(d.flapL.abs()>15))|((d.ailR*d.flapR<0)&(d.ailR.abs()>15)&(d.flapR.abs()>15))
        out.append(f"{t}: a{late.alpha.mean():4.1f} b{d.beta.abs().max():4.1f}/{late.beta.abs().mean():3.1f} vmc{d.vmcLim.min():4.2f}/{(d.vmcLim<0.99).mean()*100:3.0f}% X{cross.mean()*100:3.0f}% spl{late[['splitL','splitR']].max(axis=1).mean():4.1f} ias{d.ias.min():4.0f}>{d.ias.iloc[-1]:4.0f} dep{int((d.beta.abs()>15).any())}")
    print(f"{n:11s} "+" | ".join(out))
