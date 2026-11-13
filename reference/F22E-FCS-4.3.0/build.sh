#!/bin/bash
# usage: build.sh <srcdir> <out.dll> [DEFINES]
M="${NO_MANAGED:-/home/claude/game/Nuclear Option/NuclearOption_Data/Managed}"; B="${NO_BEPINEX_CORE:-/home/claude/game/Nuclear Option/BepInEx/core}"
SRC="$1"; OUT="$2"; DEF="$3"
# 4.1.4: the native core goes inside the plugin DLL (resource <Lib>.dll; FcsNative.Extract writes it out at load)
LIB=$(grep -o 'public const string Lib = "[A-Za-z0-9_]*"' "$SRC/FcsNative.cs" | cut -d'"' -f2); CORE="$(dirname "$0")/native/prebuilt/$LIB.dll"
[ -n "$LIB" ] && [ -f "$CORE" ] && DEF="$DEF -resource:$CORE,$LIB.dll" || echo "build.sh: no native core to embed ($CORE)" >&2
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)
dotnet "$CSC" -nologo -noconfig -nostdlib+ -target:library -langversion:9.0 -unsafe+ -optimize+ -deterministic -nowarn:CS0436,CS1701,CS1702 $DEF \
 -out:"$OUT" \
 -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/System.Core.dll" -r:"$M/netstandard.dll" \
 -r:"$M/Assembly-CSharp.dll" -r:"$M/UnityEngine.CoreModule.dll" -r:"$M/UnityEngine.PhysicsModule.dll" -r:"$M/UnityEngine.dll" -r:"$M/UnityEngine.SharedInternalsModule.dll" \
 -r:"$M/Mirage.dll" -r:"$M/Mirage.SocketLayer.dll" -r:"$M/Unity.Mathematics.dll" -r:"$M/UniTask.dll" -r:"$M/JamesFrowen.ScriptableVariables.dll" -r:"$B/BepInEx.dll" -r:"$B/0Harmony.dll" \
 $(find "$SRC" -name '*.cs' -not -path '*/bench/*' -not -path '*/DevUnlock/*' -not -path '*/tools/*' -not -path '*/studies/*' -not -path '*/obj/*' | tr '\n' ' ')
