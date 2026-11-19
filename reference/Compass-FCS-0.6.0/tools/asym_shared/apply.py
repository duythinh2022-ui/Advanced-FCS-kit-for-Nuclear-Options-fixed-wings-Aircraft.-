# apply the shared file to one jet: drop every member the shared file defines from FcsController.cs, make the class partial,
# write AsymShared.cs with the jet's namespace. usage: apply.py <dir of FcsController.cs> [--dry]
import sys,re; sys.path.insert(0,'/home/claude/w/tools'); from csm import *
dd=sys.argv[1]; dry='--dry' in sys.argv
S=open('/home/claude/w/canon/AsymShared.cs').read(); SL,sms,sd=index(S)
shared=set(n for m in sms for n in m.names)
fc=dd+'/FcsController.cs'; T=open(fc).read(); L,ms,d=index(T)
ns=re.search(r'namespace (\S+)',T).group(1)
drop=[m for m in ms if set(m.names)&shared]
partial=[m for m in drop if not set(m.names)<=shared]
for m in partial: print("PARTIAL multi-decl:",m.names, "-> keeping", [n for n in m.names if n not in shared])
keepL=[]; i=0; dset=set()
for m in drop:
    for k in range(m.start,m.end): dset.add(k)
out=[]
for k,l in enumerate(L):
    if k in dset:
        continue
    out.append(l)
# re-add the non-shared names of partially shared multi-declarations
txt="\n".join(out)
txt=re.sub(r'\binternal sealed class FcsController\b','internal sealed partial class FcsController',txt,count=1)
for m in partial:
    keep=[n for n in m.names if n not in shared]
    dl=L[m.decl]; ty=re.match(r'^(\s*(?:\w+\s+)*?[\w<>\[\],.]+)\s+\w',dl).group(1)
    txt=txt.replace("\tinternal sealed partial class FcsController\n\t{\n","\tinternal sealed partial class FcsController\n\t{\n"+ty+" "+", ".join(keep)+";\n\n",1)
txt=re.sub(r'\n{3,}','\n\n',txt)
missing=[n for n in shared if n not in d]
print(f"{dd.split('/')[-1]}: dropped {len(drop)} members ({sum(m.end-m.start for m in drop)} lines); shared names not in this jet before: {len(missing)}")
if not dry:
    open(fc,'w').write(txt)
    open(dd+'/AsymShared.cs','w').write(S.replace('namespace Vortex_FS20.FCS','namespace '+ns))
