#!/bin/bash
# usage: tools/smoke/run.sh <Vortex_FS20_FCS.dll>      (needs mono; game references as build.sh's)
# Builds the harness with the SDK's Roslyn against Mono's framework and runs it in an empty scratch directory.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"; DLL="$(readlink -f "$1")"
M="/home/claude/game/Nuclear Option/NuclearOption_Data/Managed"; B="/home/claude/game/Nuclear Option/BepInEx/core"
F=/usr/lib/mono/4.5; CSC=$(ls /usr/lib/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1); W=$(mktemp -d)
dotnet "$CSC" -nologo -noconfig -nostdlib+ -target:exe -langversion:9.0 -optimize+ -out:"$W/smoke.exe" -r:$F/mscorlib.dll -r:$F/System.dll -r:$F/System.Core.dll "$HERE/Program.cs"
cd "$W" && mono smoke.exe "$DLL" "$M" "$B"; rc=$?; ls -A "$W" | grep -v '^smoke.exe$' | sed 's/^/  (file left by an initializer: /;s/$/)/' ; exit $rc
