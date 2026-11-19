# check that the shared asymmetric-load files are identical in every mod apart from the namespace line.
# usage: checkshared.py <dirs.txt: "<key> <dir of FcsController.cs>" per line>
import sys,re,hashlib
dirs=[l.split() for l in open(sys.argv[1]) if l.strip()]
ok=True
for f in ['AsymShared.cs','AsymCap.cs','CoupledCap.cs']:
    hs={}
    for k,d in dirs:
        try: s=open(f'{d}/{f}').read()
        except FileNotFoundError: print(f"  {f}: MISSING in {k}"); ok=False; continue
        s=re.sub(r'^namespace \S+$','namespace X',s,count=1,flags=re.M)
        hs[k]=hashlib.sha256(s.encode()).hexdigest()[:16]
    vals=set(hs.values())
    print(f"{f:16s} {'IDENTICAL' if len(vals)==1 else 'DIFFERS'} in {len(hs)} mods  sha256(ns-normalised) {next(iter(vals)) if len(vals)==1 else hs}")
    ok &= len(vals)==1
# the per-jet members: names the shared file needs from each jet, and their values there
print("per-jet settings (FcsController.cs):")
for k,d in dirs:
    s=open(f'{d}/FcsController.cs').read()
    vals=[]
    for nm in ['CplRefAuth','CplKneeOn0','CplStabPeakRule','CplTopEdge','CplStabTravelLp','AsymStabTravel','AsymCapBrake']:
        m=re.search(r'internal static \w+ '+nm+r'\s*=\s*([^;]+);',s); vals.append(f"{nm}={m.group(1) if m else '-'}")
    m=re.search(r'AsymJetName = "([^"]+)"',s)
    print(f"  {k:8s} {m.group(1) if m else '?':8s} "+" ".join(vals))
sys.exit(0 if ok else 1)
