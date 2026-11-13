#!/bin/bash
# usage: build.sh <srcdir> <out.dll> [DEFINES]
# 0.3.0: the native core (native/build.sh -> Ternion_FcsCore.dll) is embedded in the plugin DLL when it has been built
# (NOEMBED=1 leaves it out: the two-file layout, the DLL beside the plugin)
M="${NO_MANAGED:-/home/claude/game/Nuclear Option/NuclearOption_Data/Managed}"; B="${NO_BEPINEX_CORE:-/home/claude/game/Nuclear Option/BepInEx/core}"
SRC="$1"; OUT="$2"; DEF="$3"
# the tested core binary: native/prebuilt/Ternion_FcsCore.dll (native/build.sh rebuilds it from the crate; copy it there)
CORE="$(dirname "$0")/native/prebuilt/Ternion_FcsCore.dll"; RES=""
if [ -z "$NOEMBED" ]; then if [ -f "$CORE" ]; then RES="-resource:$CORE,Ternion_FcsCore.dll"; else echo "build.sh: no native core to embed ($CORE)" >&2; fi; fi
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)
dotnet "$CSC" -nologo -noconfig -nostdlib+ -target:library -langversion:9.0 -unsafe+ -optimize+ -deterministic -nowarn:CS0436,CS1701,CS1702 $DEF $RES \
 -out:"$OUT" \
 -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/System.Core.dll" -r:"$M/netstandard.dll" \
 -r:"$M/Assembly-CSharp.dll" -r:"$M/UnityEngine.CoreModule.dll" -r:"$M/UnityEngine.PhysicsModule.dll" -r:"$M/UnityEngine.dll" -r:"$M/UnityEngine.SharedInternalsModule.dll" \
 -r:"$M/Mirage.dll" -r:"$M/Mirage.SocketLayer.dll" -r:"$M/Unity.Mathematics.dll" -r:"$M/UniTask.dll" -r:"$M/JamesFrowen.ScriptableVariables.dll" -r:"$B/BepInEx.dll" -r:"$B/0Harmony.dll" \
 $(find "$SRC" -name "*.cs" -not -path '*/bench/*' -not -path '*/DevUnlock/*' -not -path '*/tools/*' -not -path '*/obj/*' -not -path '*/native/*' | LC_ALL=C sort | tr "\n" " ")
