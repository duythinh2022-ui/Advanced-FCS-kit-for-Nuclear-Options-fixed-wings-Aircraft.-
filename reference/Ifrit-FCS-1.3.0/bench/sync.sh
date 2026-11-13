#!/bin/bash
# copy FCS core sources from $1 (dir containing Aryx_F22E_StrikeRaptor.FCS) into fcs/
S="$1/Aryx_F22E_StrikeRaptor.FCS"
rm -rf fcs; mkdir fcs
for f in FcsController Allocator Effector SurfaceEffector TvcEffector RateTraj Lpf Lpf3 AirfoilTable AsymCap EvalContext FcsMode SurfaceKind FlapSystem StallStagger FuelBalance Downwash BodyLift Camber; do cp "$S/$f.cs" fcs/; done


sed -i -E 's/\bprivate\b/internal/g' fcs/FcsController.cs
# 2.2.3: DragReliefMath only (the MonoBehaviour half needs the game)
python3 - "$S/DragRelief.cs" fcs/DragReliefMath.cs <<'PY'
import sys; s=open(sys.argv[1]).read(); i=s.index('\tinternal class DragRelief : MonoBehaviour'); open(sys.argv[2],'w').write(s[:i]+'}\n')
PY
