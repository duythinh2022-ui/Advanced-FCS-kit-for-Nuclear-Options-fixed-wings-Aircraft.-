#!/bin/bash
# usage: build-family.sh <out-dir>
# Vortex 0.7.1: the FS-20 Vortex Family FCS - the same source as the FS-20 Vortex FCS, built with FAMILY_FCS (its own plugin GUID
# "Vortex_FS20.FCS.Family", Harmony id, config, telemetry prefix and native core name VortexFamily_FcsCore) and the namespace
# Vortex_FS20.FCS renamed to Vortex_FS20.FamilyFCS so the two assemblies never share a type name. Writes
# VortexFamily_FS20_FCS.dll and VortexFamily_FS20_FCS.Dev.dll (the dev/debug unlock, GUID "Vortex_FS20.FCS.Family.Dev").
set -e
H="$(cd "$(dirname "$0")" && pwd)"; OUT="$1"; mkdir -p "$OUT"
python3 "$H/tools/gen_buildinfo.py" "$H/Vortex_FS20.FCS" "$H/native/fcs_core"
T=$(mktemp -d); trap 'rm -rf "$T"' EXIT
cp -r "$H/Vortex_FS20.FCS" "$T/src"; rm -rf "$T/src/obj" "$T/src/bin"
find "$T/src" -name '*.cs' -print0 | xargs -0 sed -i -e 's/\bVortex_FS20\.FCS\b/Vortex_FS20.FamilyFCS/g'
# the plugin GUIDs and the FS-20 Vortex FCS's namespace looked up by FamilyCompat stay literal strings
sed -i -e 's/"Vortex_FS20\.FamilyFCS"/"Vortex_FS20.FCS"/g' -e 's/"Vortex_FS20\.FamilyFCS\.Family"/"Vortex_FS20.FCS.Family"/g' \
       -e 's/"Vortex_FS20\.FamilyFCS\.Family\.Dev"/"Vortex_FS20.FCS.Family.Dev"/g' -e 's/"Vortex_FS20\.FamilyFCS\."/"Vortex_FS20.FCS."/g' \
       -e 's/OtherNs = "Vortex_FS20\.FCS\."/OtherNs = "Vortex_FS20.FCS."/' "$T/src"/*.cs
NO_GEN=1 bash "$H/build.sh" "$T/src" "$OUT/VortexFamily_FS20_FCS.dll" "-define:FAMILY_FCS"
mkdir -p "$T/dev"; sed -e 's/"Vortex_FS20\.FCS\.Dev"/"Vortex_FS20.FCS.Family.Dev"/' -e 's/FS-20 Vortex FCS - Dev/FS-20 Vortex Family FCS - Dev/' \
  -e 's/namespace Vortex_FS20\.FCS\.Dev/namespace Vortex_FS20.FamilyFCS.Dev/' -e 's/Vortex FCS dev\/debug/Vortex Family FCS dev\/debug/' \
  -e 's/Vortex_FS20\.FCS\.Dev\.cfg/Vortex_FS20.FCS.Family.Dev.cfg/' "$H/DevUnlock/FcsDevPlugin.cs" > "$T/dev/FcsDevPlugin.cs"
M="/home/claude/game/Nuclear Option/NuclearOption_Data/Managed"; B="/home/claude/game/Nuclear Option/BepInEx/core"
CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)
dotnet "$CSC" -nologo -noconfig -nostdlib+ -target:library -langversion:9.0 -optimize+ -deterministic -out:"$OUT/VortexFamily_FS20_FCS.Dev.dll" \
  -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/netstandard.dll" -r:"$M/UnityEngine.CoreModule.dll" -r:"$M/UnityEngine.dll" -r:"$B/BepInEx.dll" "$T/dev/FcsDevPlugin.cs"
echo "built $OUT"
