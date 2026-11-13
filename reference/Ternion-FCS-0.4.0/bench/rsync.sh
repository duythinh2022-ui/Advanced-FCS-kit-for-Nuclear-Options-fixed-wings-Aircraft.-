#!/bin/bash
# copy Revoker FCS core sources from $1 (dir containing Ternion_FS3.FCS) into fcs/
S="$1/Ternion_FS3.FCS"
rm -rf fcs; mkdir fcs
for f in FcsController EstimatorJob TelemetryRow ParallelFcs Allocator Effector SurfaceEffector TvcEffector RateTraj Lpf Lpf3 AirfoilTable AsymCap EvalContext FcsMode SurfaceKind FlapSystem StallStagger Strake FuelBalance Downwash BodyLift Camber IfritEffectors TvcYawEffector HingeFlapEffector CapacityEstimator CoupledCap FcsNative; do cp "$S/$f.cs" fcs/; done
sed -i -E 's/\bprivate\b/internal/g' fcs/FcsController.cs
sed -i -E 's/namespace Ternion_FS3\.FCS/namespace Aryx_F22E_StrikeRaptor.FCS/; s/\bTurbojet\b/Turbofan/g' fcs/*.cs
python3 - "$S/DragRelief.cs" fcs/DragReliefMath.cs <<'PY'
import sys; s=open(sys.argv[1]).read(); s=s.replace('namespace Ternion_FS3.FCS','namespace Aryx_F22E_StrikeRaptor.FCS'); i=s.index('\tinternal class DragRelief : MonoBehaviour'); open(sys.argv[2],'w').write(s[:i]+'}\n')
PY
# the Plugin stub is regenerated from the FCS Plugin.cs + the Dev config next to this script (tomrev.cfg); fields the generator
# does not know (SplitRange and any extra bench-only statics) are carried over from the previous stub
D="$(cd "$(dirname "$0")" && pwd)"; CFG="${CFG:-$D/tomtern.cfg}"; cp stubs/Plugin.cs /tmp/Plugin.stub.bak
python3 genplugin2.py "$S/Plugin.cs" "$CFG" /tmp/Plugin.gen.cs >/dev/null
python3 - <<'PY'
import re
old=open('/tmp/Plugin.stub.bak').read(); new=open('/tmp/Plugin.gen.cs').read()
nn=set(re.findall(r'internal static \S+ (\w+)\s*=',new))
oldd={re.match(r'internal static \S+ (\w+)',l).group(1):l for l in old.split('\n') if re.match(r'internal static \S+ (\w+)\s*=',l)}
extra=[l for k,l in oldd.items() if k not in nn]
L=new.split('\n')
for i,l in enumerate(L):
    m=re.match(r'internal static \S+ (\w+)',l)
    if m and m.group(1)=='SplitRange': L[i]=oldd['SplitRange']
new='\n'.join(L)
i=new.rstrip().rfind('}'); i=new.rstrip()[:i].rfind('}')
new=new[:i]+'\n'.join(extra)+'\n'+new[i:]
open('stubs/Plugin.cs','w').write(new)
PY
sed -i 's/internal static bool OnlyWhileLogging = true;/internal static bool OnlyWhileLogging = false;/' fcs/CapacityEstimator.cs
