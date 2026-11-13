#!/bin/bash
# eq9.sh <tag> [Name=value...]: eqerr of the 9 Nov logs with groups
cd /home/claude/su30/src/bench; L="/mnt/user-data/uploads/Nuclear Option/BepInEx"; export BENCH_DATA=$PWD/extract/su30_airframe.json BENCH_DIR=$PWD; G="SlipTvcAssist=false SlipGainHighAoA=0 CorkscrewSlipTol=5 AsymFFMaxBeta=12"
tag=$1; shift; rm -rf out/eq/$tag; mkdir -p out/eq/$tag
for t in 20261109_090528 20261109_095915 20261109_102540; do
  dotnet bin/bench.dll eqerr "log=$L/Su30_FCS_$t.csv" out=out/eq/$tag/$t.csv groups=1 $G "$@" 2>/dev/null
done
