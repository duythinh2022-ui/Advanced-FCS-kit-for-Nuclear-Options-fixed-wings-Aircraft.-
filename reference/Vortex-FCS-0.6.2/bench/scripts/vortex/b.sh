#!/bin/bash
# bench launcher: b.sh <mode> args...   (BENCH_BIN overrides the build dir, for A/B against a baseline build)
R=/home/claude/vortex/src/vortex; B=$R/bench
export BENCH_DIR=$B BENCH_DATA=${BENCH_DATA:-$B/extract/vortex_airframe.json} BENCH_DMG=${BENCH_DMG:-$B/data/revoker_damage.json}
exec dotnet "${BENCH_BIN:-$B/bin}/bench.dll" "$@"
