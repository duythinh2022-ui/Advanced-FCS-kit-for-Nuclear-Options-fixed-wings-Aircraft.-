import pandas as pd, numpy as np, glob, sys
L="/mnt/user-data/uploads/Nuclear Option/BepInEx/"
for f in sorted(glob.glob(L+"Ternion_FCS_*.csv")):
    d=pd.read_csv(f,usecols=lambda c:c in("t","mode","tas","alpha","beta","p","r","massKg","asymDy","tw","hlPos"),low_memory=False)
    a=d[(d["mode"]==3)&(d.tas>40)]
    if len(a)==0: print(f[-19:],"no air"); continue
    dt=np.median(np.diff(a.t))
    s=lambda m: m.sum()*dt
    print(f[-19:-4], f"air {s(np.ones(len(a),bool)):6.0f}s  mass {a.massKg.min():6.0f}-{a.massKg.max():6.0f}  |dy|>4cm {s(a.asymDy.abs()>0.04):5.0f}s  a>30 {s(a.alpha>30):5.0f}s a>40 {s(a.alpha>40):5.0f}s  a>30&|p|>30 {s((a.alpha>30)&(a.p.abs()>30)):4.0f}s  |b|>3&a>30 {s((a.alpha>30)&(a.beta.abs()>3)):4.0f}s  tw {a.tw.abs().max():.2f}")
