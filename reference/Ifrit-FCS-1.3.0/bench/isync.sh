#!/bin/bash
# copy Ifrit FCS core sources from $1 (dir containing Ifrit_KR67.FCS) into fcs/
S="$1/Ifrit_KR67.FCS"
rm -rf fcs; mkdir fcs
for f in FcsController EstimatorJob TelemetryRow ParallelFcs Allocator Effector SurfaceEffector TvcEffector RateTraj Lpf Lpf3 AirfoilTable AsymCap CoupledCap EvalContext FcsMode SurfaceKind FlapSystem StallStagger FuelBalance Downwash BodyLift Camber IfritEffectors CapacityEstimator FcsNative; do cp "$S/$f.cs" fcs/; done
sed -i -E 's/\bprivate\b/internal/g' fcs/FcsController.cs
sed -i -E 's/namespace Ifrit_KR67\.FCS/namespace Aryx_F22E_StrikeRaptor.FCS/; s/\bTurbojet\b/Turbofan/g' fcs/*.cs
python3 - "$S/DragRelief.cs" fcs/DragReliefMath.cs <<'PY'
import sys; s=open(sys.argv[1]).read(); s=s.replace('namespace Ifrit_KR67.FCS','namespace Aryx_F22E_StrikeRaptor.FCS'); i=s.index('\tinternal class DragRelief : MonoBehaviour'); open(sys.argv[2],'w').write(s[:i]+'}\n')
PY

# 31 Oct calibration: the bench renames the Ifrit surfaces (Airframe.IfritNames); give Camber.HostOf the game name
sed -i 's/Camber.HostOf(seH.cs.gameObject.name)/Camber.HostOf(Airframe.GameName(seH.cs.gameObject.name))/' fcs/FcsController.cs
sed -i 's/internal static bool OnlyWhileLogging = true;/internal static bool OnlyWhileLogging = false;/' fcs/CapacityEstimator.cs
