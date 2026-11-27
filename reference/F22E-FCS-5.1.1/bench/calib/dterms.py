#!/usr/bin/env python3
# dterms.py <derivs.py output> term1,term2 [axes] — game|bench|ratio of chosen terms per axis and AoA band
import sys, re
txt = open(sys.argv[1]).read(); terms = sys.argv[2].split(","); axes = sys.argv[3].split(",") if len(sys.argv) > 3 else ["roll", "pitch", "yaw"]
for blk in txt.split("==== ")[1:]:
    ax = blk.split(":")[0]
    if ax not in axes: continue
    lines = blk.strip().split("\n"); hdr = lines[1].split(); names = hdr[2:-4]
    print(f"[{ax}]")
    for l in lines[2:]:
        m0 = re.match(r"\s*(-?\d+)\.\.\s*(-?\d+)\s+(\d+)\s+(.*)", l)
        if not m0: continue
        vals = re.findall(r"(-?[\d.]+|nan)\|\s*(-?[\d.]+|nan)\|\s*(-?[\d.]+|nan)", m0.group(4))
        d = dict(zip(names, vals))
        out = f"  {m0.group(1):>4}..{m0.group(2):>4} n={m0.group(3):>6}"
        for t in terms:
            if t in d: g, b, r = d[t]; out += f"   {t} {g:>7}|{b:>7}|{r:>5}"
        print(out)
