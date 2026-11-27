#!/bin/bash
# usage: build.sh <srcdir> <out.dll> [DEFINES]
# kit 5.0: two build layouts
#   default (split):  the plugin DLL only; the native core ships beside it as a separate <Lib>.dll (copied next to <out.dll>).
#                     Without that file the plugin runs its C# twin of the core (same laws, slower). NOMM wants this layout.
#   UNIFIED=1:        the native core embedded in the plugin DLL (the single-file layout the releases up to kit 4.1 shipped).
#   NOEMBED=1 is accepted as the old name of the split layout.
# game reference DLLs: NO_GAME=<your Nuclear Option folder>, or NO_MANAGED=<...Managed> and NO_BEPINEX_CORE=<...BepInEx/core>
G="${NO_GAME:-/home/claude/game/Nuclear Option}"; M="${NO_MANAGED:-$G/NuclearOption_Data/Managed}"; B="${NO_BEPINEX_CORE:-$G/BepInEx/core}"
[ -f "$M/Assembly-CSharp.dll" ] || { echo "build.sh: the game's DLLs are not in '$M'. Set NO_GAME to your Nuclear Option folder (or NO_MANAGED / NO_BEPINEX_CORE)." >&2; exit 1; }
[ -f "$B/BepInEx.dll" ] || { echo "build.sh: BepInEx.dll is not in '$B'. Install BepInEx 5 in the game, or set NO_BEPINEX_CORE." >&2; exit 1; }
[ "$NOEMBED" = 1 ] && UNIFIED=0
SRC="$1"; OUT="$2"; DEF="$3"
# 1.1.1: the native core goes inside the plugin DLL (resource <Lib>.dll; FcsNative.Extract writes it out at load)
# 0.7.1: the FS-20 Vortex Family FCS (-define:FAMILY_FCS) ships its native core as a separate file, so nothing is embedded there.
# FcsBuildInfo.cs (the family fingerprint) is regenerated from the tree unless NO_GEN=1 (build-family.sh generates it before renaming).
[ "$NO_GEN" = 1 ] || python3 "$(dirname "$0")/tools/gen_buildinfo.py" "$SRC" "$(dirname "$0")/native/fcs_core" || exit 1
if [[ "$DEF" == *FAMILY_FCS* ]]; then echo "build.sh: family build, native core not embedded" >&2; else
LIB=$(grep -o 'public const string Lib = "Vortex_FcsCore"' "$SRC/FcsNative.cs" | cut -d'"' -f2); CORE="$(dirname "$0")/native/prebuilt/$LIB.dll"
[ "$UNIFIED" = 1 ] && [ -n "$LIB" ] && [ -f "$CORE" ] && DEF="$DEF -resource:$CORE,$LIB.dll" || { [ "$UNIFIED" = 1 ] && echo "build.sh: no native core to embed ($CORE)" >&2; }; fi
CSC=${CSC:-$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll /usr/share/dotnet/sdk/*/Roslyn/bincore/csc.dll "$HOME"/.dotnet/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -1)}
[ -f "$CSC" ] || { echo "build.sh: no C# compiler found. Install the .NET 8 SDK (or set CSC to a csc.dll)." >&2; exit 1; }
dotnet "$CSC" -nologo -noconfig -nostdlib+ -target:library -langversion:9.0 -unsafe+ -optimize+ -deterministic -nowarn:CS0436,CS1701,CS1702 $DEF \
 -out:"$OUT" \
 -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/System.Core.dll" -r:"$M/netstandard.dll" \
 -r:"$M/Assembly-CSharp.dll" -r:"$M/UnityEngine.CoreModule.dll" -r:"$M/UnityEngine.PhysicsModule.dll" -r:"$M/UnityEngine.dll" -r:"$M/UnityEngine.SharedInternalsModule.dll" \
 -r:"$M/Mirage.dll" -r:"$M/Mirage.SocketLayer.dll" -r:"$M/Unity.Mathematics.dll" -r:"$M/UniTask.dll" -r:"$M/JamesFrowen.ScriptableVariables.dll" -r:"$M/Rewired_Core.dll" -r:"$M/Unity.TextMeshPro.dll" -r:"$M/UnityEngine.UI.dll" -r:"$M/UnityEngine.UIModule.dll" -r:"$M/UnityEngine.TextRenderingModule.dll" -r:"$M/UnityEngine.InputLegacyModule.dll" -r:"$B/BepInEx.dll" -r:"$B/0Harmony.dll" \
 $(find "$SRC" -name "*.cs" -not -path '*/bench/*' -not -path '*/DevUnlock/*' -not -path '*/tools/*' -not -path '*/obj/*' | LC_ALL=C sort | tr "\n" " ")
RC=$?; [ $RC = 0 ] || exit $RC
if [ "$UNIFIED" != 1 ] && [[ "$DEF" != *FAMILY_FCS* ]]; then
  LIB=${LIB:-$(grep -o 'public const string Lib = "[A-Za-z0-9_]*"' "$SRC/FcsNative.cs" | cut -d'"' -f2)}; CORE=${CORE:-"$(dirname "$0")/native/prebuilt/$LIB.dll"}
  if [ -f "$CORE" ]; then cp -f "$CORE" "$(dirname "$OUT")/$LIB.dll" && echo "build.sh: split layout: $(basename "$OUT") + $LIB.dll (install both in the same plugin folder; UNIFIED=1 embeds the core instead)" >&2
  else echo "build.sh: split layout, but no native core at $CORE: the plugin will run its C# solver" >&2; fi
fi
