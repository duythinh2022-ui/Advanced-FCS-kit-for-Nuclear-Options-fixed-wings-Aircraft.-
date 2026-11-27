#!/bin/bash
# leaf.sh tag extra... : full aft + full roll at the limit; hold, reversal, release. entry 250/350 km/h
cd /home/claude/su30/v10/src/bench; export BENCH_DATA=$PWD/extract/su30_airframe.json
tag=$1; shift
run(){ c=$1; ias=$2; P=$3; Rr=$4; T=$5; shift 5; timeout 900 dotnet bin/bench.dll ifly ias=$ias alt=3000 thr=1 pitch="$P" roll="$Rr" T=$T fcslog=runs/${tag}_${c}_${ias}.csv "$@" > runs/${tag}_${c}_${ias}.out 2>&1; }
for ias in 250 350; do
 run lf  $ias "0:0,0.3:0,0.4:1,30:1" "0:0,0.3:0,0.4:-1,14:-1,14.05:1,20:1,20.05:0" 24 "$@" &
done; wait
