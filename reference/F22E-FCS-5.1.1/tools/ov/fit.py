# replay match to the game per stab multiplier: AoA, q and stab-position error over all windows
import pandas as pd, numpy as np, json, sys
W=json.load(open("windows.json"))
for tag in sys.argv[1:]:
    E=[]
    for w in W:
        d=pd.read_csv(f"out/{tag}/w{w['id']:02d}.csv"); d=d[d.t<=d.t.iloc[0]+2.5]   # first 2.5 s: before the runs drift apart
        sb=(d.stabL_b+d.stabR_b)/2; sg=(d.stabL_g+d.stabR_g)/2
        E.append(dict(eA=(d.alpha_b-d.alpha_g).abs().mean(), eQ=(d.q_b-d.q_g).abs().mean(), eS=(sb-sg).abs().mean(), bS=(sb-sg).mean(), pk=d.alpha_b.max()-d.alpha_g.max()))
    E=pd.DataFrame(E); print(f"{tag:14s} |AoA err| {E.eA.median():.2f}  |q err| {E.eQ.median():.2f}  |stab sym err| {E.eS.median():.2f} (bias {E.bS.median():+.2f})  peak err median {E.pk.median():+.2f} mean|.| {E.pk.abs().mean():.2f}")
