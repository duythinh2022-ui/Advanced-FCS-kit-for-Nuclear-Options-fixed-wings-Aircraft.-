# score.py <tag> <ref> <cg> : per-approach acceptance against the reference config (4.8.13), Tom's 26 Nov criterion
import os as _o, sys as _s; _s.path.insert(0, _o.path.dirname(_o.path.abspath(__file__))); from _paths import *; import sys  # kit 5.0: paths relative to this tree
import pandas as pd, numpy as np, json, sys
tag, ref, cg = sys.argv[1], sys.argv[2], sys.argv[3]; verbose = len(sys.argv) > 4
W = json.load(open(OV+"/windows.json"))
def stats(t, w):
    d = pd.read_csv(f"{OV}/out/{t}_{cg}/w{w['id']:02d}.csv")
    return dict(pk=d.alpha_b.max(), roll=d.p_b.abs().mean(), slip=d.beta_b.abs().max(), gpk=d.alpha_g.max())
rows=[]
for w in W:
    a, b = stats(ref, w), stats(tag, w)
    rows.append(dict(id=w['id'], t=w['t40'], gpk=a['gpk'], rpk=a['pk'], pk=b['pk'], dpk=b['pk']-a['pk'], rroll=a['roll'], droll=b['roll']-a['roll'], rslip=a['slip'], slip=b['slip']))
R = pd.DataFrame(rows)
ov = R[R.rpk > 55.5]; ot = R[R.rpk <= 55.5]
fixed = (ov.pk <= 55.5).sum()
big = (ot.dpk.abs() > 1).sum(); newov = (ot.pk > 55.5).sum()
print(f"{tag} vs {ref} CG {cg}: ref overshoots {len(ov)} -> fixed {fixed}/{len(ov)} (worst {ov.pk.max() if len(ov) else 0:.1f}, mean {ov.pk.mean() if len(ov) else 0:.2f}) | others {len(ot)}: |dpk| mean {ot.dpk.abs().mean():.2f} max {ot.dpk.abs().max():.2f}, >1deg {big}, new overshoot {newov}, |droll| mean {ot.droll.abs().mean():.2f} max {ot.droll.abs().max():.1f} | overshoot-case droll mean {ov.droll.mean() if len(ov) else 0:+.1f}")
if verbose: print(R[(R.dpk.abs()>1)|(R.rpk>55.5)|(R.droll.abs()>3)].round(2).to_string())
