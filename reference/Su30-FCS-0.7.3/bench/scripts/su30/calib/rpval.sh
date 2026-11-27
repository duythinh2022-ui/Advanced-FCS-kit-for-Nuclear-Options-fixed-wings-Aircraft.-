#!/bin/bash
# rpval.sh <tag> [Name=value...]: closed-loop replays of held-out high-AoA windows with 0.1.8 settings
cd /home/claude/su30/src/bench; L="/mnt/user-data/uploads/Nuclear Option/BepInEx"; export BENCH_DATA=$PWD/extract/su30_airframe.json BENCH_DIR=$PWD
G="SlipTvcAssist=false SlipGainHighAoA=0 CorkscrewSlipTol=5 AsymFFMaxBeta=12"; S18="PitchWallSurfacePeak=false GCaptureReachable=false HighAoACanardPitchFirst=1000"
tag=$1; shift; mkdir -p runs/val
for w in "095915 89 94" "095915 155 160.5" "095915 194 198.5" "095915 386 391.5" "102540 43 49" "102540 119 124.5" "102540 193.8 200.5"; do
 set -- $w "$@"; t=$1; a=$2; b=$3; shift 3
 dotnet bin/bench.dll replay "log=$L/Su30_FCS_20261109_$t.csv" t0=$a t1=$b out=runs/val/${tag}_${t}_$a.csv $G $S18 "$@" 2>/dev/null >/dev/null
done
