import pandas as pd, numpy as np, sys, glob
for f in sorted(glob.glob(sys.argv[1])):
    d=pd.read_csv(f)
    print(f"{f.split('/')[-1]:14s} beta pk bench {d.beta_b.abs().max():5.1f} game {d.beta_g.abs().max():5.1f} | p rms err {np.sqrt(((d.p_b-d.p_g)**2).mean()):5.1f} r rms {np.sqrt(((d.r_b-d.r_g)**2).mean()):5.1f} alpha rms {np.sqrt(((d.alpha_b-d.alpha_g)**2).mean()):4.1f} | |p| max b/g {d.p_b.abs().max():4.0f}/{d.p_g.abs().max():4.0f}  pSmax min {d.pSmax.min():4.0f}")
