# one line per tag: per case "hmin/hmax (vsmin/vsmax)"   usage: vsum.py tag...
import sys, importlib.util
sp=importlib.util.spec_from_file_location('vsan','/home/claude/vortex/tools/vsan.py'); 
import pandas as pd, numpy as np
src=open('/home/claude/vortex/tools/vsan.py').read().split("tags=sys.argv")[0]; exec(src)
for tag in sys.argv[1:]:
    out=[]; worst=0
    for c in W:
        r=an(c,tag)
        if r is None: out.append(f"{c}: -"); continue
        worst=max(worst,abs(r['emin']),abs(r['emax']))
        out.append(f"{c} {r['emin']:+.2f}/{r['emax']:+.2f} ({r['vmin']:+.2f}/{r['vmax']:+.2f}) o{r['tout']:.1f}")
    print(f"{tag:12s} worst {worst:.2f} | "+' | '.join(out))
