#!/bin/bash
# eqrun.sh <tag> [Name=value...]: eqerr of all logs
cd /home/claude/su30/src/bench; L="/mnt/user-data/uploads/Nuclear Option/BepInEx"; export BENCH_DATA=$PWD/extract/su30_airframe.json BENCH_DIR=$PWD; G="SlipTvcAssist=false SlipGainHighAoA=0 CorkscrewSlipTol=5 AsymFFMaxBeta=12"
tag=$1; shift; mkdir -p out/eq/$tag
for t in 20261108_002253 20261108_003246 20261108_013817 20261108_015821 20261109_090528 20261109_095915 20261109_102540; do
  dotnet bin/bench.dll eqerr "log=$L/Su30_FCS_$t.csv" out=out/eq/$tag/$t.csv $G "$@" 2>/dev/null
done
