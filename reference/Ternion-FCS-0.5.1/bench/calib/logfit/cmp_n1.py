import sys, numpy as np, pandas as pd
TAGS=__import__("sys").argv[1:] or ["b","n"]
g = pd.read_csv("cal/n1.csv", low_memory=False)
def stats(d, t0, t1):
    x = d[(d.t >= t0) & (d.t < t1)]
    return x
def row(nm, x):
    capcol = "asymCapEff"
    return (f"{nm:>5} a {x.alpha.mean():5.1f} [{x.alpha.min():5.1f},{x.alpha.max():5.1f}] cap {x[capcol].min():5.1f}-{x[capcol].max():5.1f} "
            f"capP {x.asymCapPitch.min():6.1f} q_sd {x.q.std():4.1f} qCmd<0 {((x.qCmd<-1)&(x.sp>0.5)).mean():4.2f} |b|max {x.beta.abs().max():4.1f} ias {x.ias.min():5.1f}-{x.ias.max():5.1f}")
for w0, w1 in [(118,134),(156,182),(236,260),(82,116),(182,236)]:
    print(f"== {w0}-{w1}")
    print(row("GAME", stats(g, w0, w1)))
    for tag in TAGS:
        d = pd.read_csv(f"cal/rp/n1_{tag}_{w0}.csv"); print(row(tag, stats(d, w0, w1)))
    # 2-s detail
    for t0 in np.arange(w0, w1, 2.0):
        line = f"   {t0:5.0f}"
        for nm, d in [("G", g)] + [(t, pd.read_csv(f"cal/rp/n1_{t}_{w0}.csv")) for t in TAGS]:
            x = d[(d.t >= t0) & (d.t < t0 + 2)]
            if len(x): line += f" | {nm} a{x.alpha.mean():5.1f} cap{x.asymCapEff.mean():5.1f} q{x.q.mean():+5.1f} sp{x.sp.mean():+.1f} ias{x.ias.mean():4.0f}"
        print(line)
