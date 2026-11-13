#!/bin/bash
# copy Ifrit FCS core sources from $1 (dir containing Ifrit_KR67.FCS) into fcs/
S="$1/Ifrit_KR67.FCS"
rm -rf fcs; mkdir fcs
for f in FcsController Allocator Effector SurfaceEffector TvcEffector RateTraj Lpf Lpf3 AirfoilTable AsymCap EvalContext FcsMode SurfaceKind FlapSystem StallStagger FuelBalance Downwash BodyLift Camber IfritEffectors; do cp "$S/$f.cs" fcs/; done
sed -i -E 's/\bprivate\b/internal/g' fcs/FcsController.cs
sed -i -E 's/namespace Ifrit_KR67\.FCS/namespace Aryx_F22E_StrikeRaptor.FCS/; s/\bTurbojet\b/Turbofan/g' fcs/*.cs
python3 - "$S/DragRelief.cs" fcs/DragReliefMath.cs <<'PY'
import sys; s=open(sys.argv[1]).read(); s=s.replace('namespace Ifrit_KR67.FCS','namespace Aryx_F22E_StrikeRaptor.FCS'); i=s.index('\tinternal class DragRelief : MonoBehaviour'); open(sys.argv[2],'w').write(s[:i]+'}\n')
PY
