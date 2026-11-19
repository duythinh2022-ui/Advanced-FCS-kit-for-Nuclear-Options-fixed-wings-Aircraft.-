#!/bin/bash
# sync FCS sources into bench/fcs, regenerate the Plugin stub (Tom's cfg + dependency order), build
cd "$(dirname "$0")/bench"
bash sync.sh .. >/dev/null
python3 genplugin2.py ../Aryx_F22E_StrikeRaptor.FCS/Plugin.cs ../tomcfg.cfg stubs/Plugin.cs >/dev/null
python3 - <<'PY'
import re
s=open("stubs/Plugin.cs").read().split("\n")
head=[];fields=[];tail=[]
pat=re.compile(r'^internal static [\w<>\[\]\.]+ (\w+) = (.*);$')
st=0
for l in s:
    m=pat.match(l)
    if m: fields.append((m.group(1),l)); st=1
    elif st==0: head.append(l)
    else: tail.append(l)
names={n for n,_ in fields}
deps={n:{x for x in re.findall(r'\b([A-Z]\w+)\b',l.split('=',1)[1]) if x in names and x!=n} for n,l in fields}
order=[];done=set()
def visit(n):
    if n in done: return
    for d in deps[n]: visit(d)
    done.add(n); order.append(n)
for n,_ in fields: visit(n)
L=dict(fields); open("stubs/Plugin.cs","w").write("\n".join(head+[L[n] for n in order]+tail))
PY
dotnet build -c Release -o bin 2>&1 | grep -E " error |Build succeeded"
# 4.0.0: the native allocator core for the bench (Linux build of native/fcs_core); without cargo the bench runs the C# solve
(cd ../native/fcs_core && command -v cargo >/dev/null && cargo build --release --offline 2>&1 | grep -E "^error|warning: unused|Finished" ; cp -f target/release/libF22E_FcsCore.so ../../bench/bin/ 2>/dev/null) || true
